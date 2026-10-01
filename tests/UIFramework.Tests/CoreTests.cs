using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public sealed class MemoryElement : IUIElement
{
    public bool Visible, Interactive;
    public int Disposals;
    public Exception? DisposeFailure;
    public Exception? EnableFailure;
    public void SetVisible(bool value) => Visible = value;
    public void SetInteractive(bool value)
    {
        if (value && EnableFailure != null) throw EnableFailure;
        Interactive = value;
    }
    public void BringToFront() { }
    public ValueTask DisposeAsync()
    {
        Disposals++;
        if (DisposeFailure != null) throw DisposeFailure;
        return default;
    }
}

public sealed class TestHost : IUIHost, IUILog, IUIResourceLoader
{
    public readonly List<Exception> Errors = new();
    public readonly List<MemoryElement> Loaded = new();
    public Exception? LoadFailure;
    public TaskCompletionSource<bool>? LoadGate;
    public object? Focus;
    public void VerifyAccess() { }
    public object? CaptureFocus() => Focus;
    public void FocusElement(IUIElement? element) => Focus = element;
    void IUIHost.Focus(IUIElement? element) => FocusElement(element);
    public void RestoreFocus(object? focus) => Focus = focus;
    public void Error(Exception exception, string context) => Errors.Add(exception);
    public async Task<IUIElement> LoadAsync(string resource, IUIElement? mount, CancellationToken token)
    {
        if (LoadGate != null) await LoadGate.Task.WaitAsync(token);
        if (LoadFailure != null) throw LoadFailure;
        token.ThrowIfCancellationRequested();
        var element = new MemoryElement(); Loaded.Add(element); return element;
    }
}

public class CounterView : UINode
{
    public int Initialized, Entered, Exited;
    public Exception? InitializeFailure, EnterFailure, DisposeFailure;
    protected override Task OnInitializeAsync(CancellationToken token)
    { Initialized++; if (InitializeFailure != null) throw InitializeFailure; return Task.CompletedTask; }
    protected override Task OnEnterAsync(CancellationToken token)
    { Entered++; if (EnterFailure != null) throw EnterFailure; return Task.CompletedTask; }
    protected override Task OnExitAsync(CancellationToken token) { Exited++; return Task.CompletedTask; }
    protected override Task OnDisposeAsync() { if (DisposeFailure != null) throw DisposeFailure; return Task.CompletedTask; }
}

public sealed class ArgumentView : UINode<string> { }
public sealed class BoolDialog : DialogNode<bool> { }
public sealed class ReentrantView : UINode
{
    protected override async Task OnEnterAsync(CancellationToken token) => await Context.DisposeAsync();
}
public sealed class SlowView : UINode
{
    protected override Task OnEnterAsync(CancellationToken token) => Task.Delay(Timeout.Infinite, token);
}
public sealed class FailureTransition(Exception failure) : IUITransition
{
    public Task PlayAsync(IUIElement? element, bool entering, CancellationToken token) => throw failure;
}

public class CoreTests
{
    private static (UIContext UI, TestHost Host) Create()
    {
        var host = new TestHost(); return (new UIContext(host, host, host), host);
    }
    private static async Task<T> Attach<T>(UIContext ui) where T : UINode, new()
    {
        var node = new T(); await node.AttachAsync(ui.Root, typeof(T).Name); return node;
    }

    [Fact] public async Task RootIsStableAndHasNoProjectChildren()
    {
        var (ui, _) = Create(); Assert.Same(ui.Root, ui.Root); Assert.Empty(ui.Root.Children);
        Assert.True(ui.Root.IsVisible); await ui.DisposeAsync(); Assert.Equal(NodeState.Disposed, ui.Root.State);
    }

    [Fact] public async Task EmbeddedTabsDoNotLoadOrCloneAndKeepInstances()
    {
        var (ui, host) = Create(); var tabs = await Attach<TabContainerNode>(ui);
        var a = new CounterView(); var b = new CounterView();
        var displayA = new MemoryElement { Visible = true }; var displayB = new MemoryElement { Visible = true };
        await tabs.BindAsync("A", a, displayA); await tabs.BindAsync("B", b, displayB);
        Assert.False(displayA.Visible); Assert.False(displayB.Visible);
        await tabs.SwitchAsync("A"); await tabs.SwitchAsync("B"); await tabs.SwitchAsync("A");
        Assert.Same(a, tabs.Current); Assert.True(displayA.Visible); Assert.False(displayB.Visible);
        Assert.Equal(1, a.Initialized); Assert.Equal(2, a.Entered); Assert.Empty(host.Loaded);
        await ui.DisposeAsync(); Assert.Equal(1, displayA.Disposals); Assert.Equal(1, displayB.Disposals);
    }

    [Fact] public async Task TabsAcceptSeveralBaseNodesAndRejectUnknownOrDuplicateNames()
    {
        var (ui, host) = Create(); var tabs = await Attach<TabContainerNode>(ui);
        await tabs.BindAsync("Graphics", new UINode()); await tabs.BindAsync("Audio", new UINode());
        await tabs.SwitchAsync("Graphics");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => tabs.SwitchAsync("missing"));
        Assert.Equal("Graphics", tabs.CurrentName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tabs.BindAsync("Audio", new UINode()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tabs.SwitchAsync<UINode>());
        Assert.Equal(3, host.Errors.Count); await ui.DisposeAsync();
    }

    [Fact] public async Task StackReturnsSameInstanceAndDisposesOnlyPoppedPage()
    {
        var (ui, _) = Create(); ui.Register<CounterView>(); var stack = await Attach<StackContainerNode>(ui);
        var first = await stack.PushAsync<CounterView>(); var second = await stack.PushAsync<CounterView>();
        Assert.False(first.IsVisible); Assert.True(second.IsVisible);
        await stack.PopAsync(); Assert.Same(first, stack.Current); Assert.True(first.IsVisible);
        Assert.Equal(NodeState.Disposed, second.State); Assert.Equal(1, first.Initialized);
        await stack.PopAsync(); Assert.Empty(stack.History);
        await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PopAsync()); await ui.DisposeAsync();
    }

    [Fact] public async Task IndependentCloseTargetsExactInstance()
    {
        var (ui, _) = Create(); ui.Register<CounterView>(); var panels = await Attach<PanelContainerNode>(ui);
        var first = await panels.OpenAsync<CounterView>(); var second = await panels.OpenAsync<CounterView>();
        await panels.CloseAsync(first); Assert.True(second.IsVisible); Assert.Equal(NodeState.Ready, second.State);
        Assert.Single(panels.Children); await ui.DisposeAsync();
    }

    [Fact] public async Task FailedLoadPreservesOriginalExceptionAndCurrentPage()
    {
        var (ui, host) = Create(); ui.Register<CounterView>("page"); var stack = await Attach<StackContainerNode>(ui);
        var first = await stack.PushAsync<CounterView>(); var error = new IOException("raw load error"); host.LoadFailure = error;
        var received = await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<CounterView>());
        Assert.Same(error, received); Assert.Same(first, stack.Current); Assert.True(first.IsVisible);
        Assert.Single(host.Errors); Assert.Contains("LoadAsync", received.StackTrace); await ui.DisposeAsync();
    }

    [Fact] public async Task FailedEnterCleansNewNodeWithoutCommittingHistory()
    {
        var (ui, host) = Create(); var stack = await Attach<StackContainerNode>(ui);
        ui.Register<CounterView>(); var first = await stack.PushAsync<CounterView>();
        var error = new InvalidOperationException("enter failed");
        ui.Register<ArgumentView>(() => new ArgumentView { Transition = new FailureTransition(error) }, "page");
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PushAsync<ArgumentView>("args")));
        Assert.Single(stack.History); Assert.Single(stack.Children); Assert.True(first.IsVisible);
        Assert.Equal(1, host.Loaded.Single().Disposals); await ui.DisposeAsync();
    }

    [Fact] public async Task CleanupFailureIsAttachedToOriginalRatherThanReplacingIt()
    {
        var (ui, host) = Create(); var panels = await Attach<PanelContainerNode>(ui);
        var primary = new IOException("enter"); var cleanup = new InvalidOperationException("dispose");
        ui.Register<CounterView>(() => new CounterView { EnterFailure = primary, DisposeFailure = cleanup }, "page");
        var received = await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>());
        Assert.Same(primary, received); Assert.Contains(cleanup, UIFailure.GetSecondaryExceptions(received));
        Assert.Contains(primary, host.Errors); Assert.Contains(cleanup, host.Errors); Assert.Empty(panels.Children);
        await ui.DisposeAsync();
    }

    [Fact] public async Task ConcurrentOperationThrowsInsteadOfSkipping()
    {
        var (ui, host) = Create(); ui.Register<CounterView>("page"); var stack = await Attach<StackContainerNode>(ui);
        host.LoadGate = new TaskCompletionSource<bool>(); var pending = stack.PushAsync<CounterView>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PushAsync<CounterView>());
        host.LoadGate.SetResult(true); await pending; Assert.Single(stack.History); await ui.DisposeAsync();
    }

    [Fact] public async Task DisposingScopeCancelsLoadAndWaitsForCleanup()
    {
        var (ui, host) = Create(); ui.Register<CounterView>("page"); var stack = await Attach<StackContainerNode>(ui);
        host.LoadGate = new TaskCompletionSource<bool>(); var pending = stack.PushAsync<CounterView>();
        await ui.DisposeAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Empty(host.Loaded); Assert.Empty(ui.Root.Children); Assert.Equal(NodeState.Disposed, stack.State);
    }

    [Fact] public async Task NestedModalBlocksAllOtherInputAndRestoresFocus()
    {
        var (ui, host) = Create(); ui.Register<CounterView>();
        var panels = await Attach<PanelContainerNode>(ui); var page = await panels.OpenAsync<CounterView>();
        var modals = await Attach<ModalContainerNode>(ui); var initial = new object(); host.Focus = initial;
        var a = await modals.OpenAsync<CounterView>(); Assert.False(page.IsInteractive);
        var b = await modals.OpenAsync<CounterView>(); Assert.False(a.IsInteractive); Assert.True(b.IsInteractive);
        await modals.CloseAsync(b); Assert.True(a.IsInteractive); Assert.False(page.IsInteractive);
        await modals.CloseAsync(a); Assert.True(page.IsInteractive); Assert.Same(initial, host.Focus);
        await ui.DisposeAsync();
    }

    [Fact] public async Task DialogResultReturnsOnlyAfterClosingInstance()
    {
        var (ui, _) = Create(); ui.Register<BoolDialog>(); var modals = await Attach<ModalContainerNode>(ui);
        var pending = modals.ShowDialogAsync<BoolDialog, bool>(); var dialog = Assert.IsType<BoolDialog>(modals.Current);
        dialog.Complete(false); Assert.False(await pending); Assert.Null(modals.Current);
        Assert.Equal(NodeState.Disposed, dialog.State); await ui.DisposeAsync();
    }

    [Fact] public async Task CallerCancellationClosesWaitingDialogAndPropagatesCancellation()
    {
        var (ui, _) = Create(); ui.Register<BoolDialog>(); var modals = await Attach<ModalContainerNode>(ui);
        using var cancel = new CancellationTokenSource(); var pending = modals.ShowDialogAsync<BoolDialog, bool>(cancellationToken: cancel.Token);
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(modals.Current); Assert.Null(ui.InputOwner); await ui.DisposeAsync();
    }

    [Fact] public async Task FailedInitializeDestroysDisplayAndDoesNotLeaveChild()
    {
        var (ui, host) = Create(); var panels = await Attach<PanelContainerNode>(ui);
        var error = new IOException("initialize"); ui.Register<CounterView>(() => new CounterView { InitializeFailure = error }, "page");
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>()));
        Assert.Empty(panels.Children); Assert.Equal(1, host.Loaded.Single().Disposals); await ui.DisposeAsync();
    }

    [Fact] public async Task ReattachDoesNotDestroyExistingNodeOrChangeContext()
    {
        var (ui, _) = Create(); var (other, _) = Create(); var node = await Attach<PanelContainerNode>(ui);
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.AttachAsync(other.Root, "bad"));
        Assert.Same(ui, node.Context); Assert.Same(ui.Root, node.Parent); Assert.Equal(NodeState.Ready, node.State);
        await ui.DisposeAsync(); await other.DisposeAsync();
    }

    [Fact] public void ContainerCapabilitiesAreSeparatedByType()
    {
        Assert.Null(typeof(StackContainerNode).GetMethod("SwitchAsync"));
        Assert.DoesNotContain(typeof(TabContainerNode).GetMethods(), method => method.Name == "PushAsync");
        Assert.DoesNotContain(typeof(UINode).GetMethods(), method => method.Name is "PushAsync" or "SwitchAsync" or "OpenAsync");
    }

    [Fact] public async Task LifecycleCannotDisposeItsOwnerAndDeadlock()
    {
        var (ui, _) = Create(); ui.Register<ReentrantView>(); var stack = await Attach<StackContainerNode>(ui);
        await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PushAsync<ReentrantView>().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Empty(stack.History); Assert.Empty(stack.Children); await ui.DisposeAsync();
    }

    [Fact] public async Task CancelingAnEnterCleansItsInstanceAndLeavesNoHistory()
    {
        var (ui, _) = Create(); ui.Register<SlowView>(); var stack = await Attach<StackContainerNode>(ui);
        using var cancel = new CancellationTokenSource(); var pending = stack.PushAsync<SlowView>(cancellationToken: cancel.Token);
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Empty(stack.History); Assert.Empty(stack.Children); await ui.DisposeAsync();
    }

    [Fact] public async Task ScopeDisposalEndsWaitingDialogWithoutLeavingInputOwner()
    {
        var (ui, _) = Create(); ui.Register<BoolDialog>(); var modals = await Attach<ModalContainerNode>(ui);
        var pending = modals.ShowDialogAsync<BoolDialog, bool>();
        await ui.DisposeAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(ui.InputOwner); Assert.Empty(ui.Root.Children);
    }

    [Fact] public async Task AllChildrenAreCleanedEvenWhenSeveralDisposalsFail()
    {
        var (ui, _) = Create(); ui.Register<CounterView>(); var panels = await Attach<PanelContainerNode>(ui);
        var a = await panels.OpenAsync<CounterView>(); var b = await panels.OpenAsync<CounterView>();
        var first = new IOException("B dispose"); var second = new InvalidOperationException("A dispose");
        a.DisposeFailure = second; b.DisposeFailure = first;
        Assert.Same(first, await Assert.ThrowsAsync<IOException>(async () => await ui.DisposeAsync()));
        Assert.Contains(second, UIFailure.GetSecondaryExceptions(first)); Assert.Empty(ui.Root.Children);
        Assert.Equal(NodeState.Disposed, a.State); Assert.Equal(NodeState.Disposed, b.State);
    }

    [Fact] public async Task InvalidDetachedParentThrowsExplicitErrorRatherThanNullReference()
    {
        var child = new UINode();
        await Assert.ThrowsAsync<InvalidOperationException>(() => child.AttachAsync(new UINode(), "child"));
        Assert.Equal(NodeState.Detached, child.State);
    }

    [Fact] public async Task InteractionRestoreFailureCannotReplaceOriginalLoadException()
    {
        var (ui, host) = Create(); ui.Register<CounterView>("page");
        var element = new MemoryElement(); var panels = new PanelContainerNode();
        await panels.AttachAsync(ui.Root, "Panels", element);
        var primary = new IOException("load"); var secondary = new InvalidOperationException("restore input");
        host.LoadFailure = primary; element.EnableFailure = secondary;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>()));
        Assert.Contains(secondary, UIFailure.GetSecondaryExceptions(primary));
        element.EnableFailure = null; await ui.DisposeAsync();
    }
}
