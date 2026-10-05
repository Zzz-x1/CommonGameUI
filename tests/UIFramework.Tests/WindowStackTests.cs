using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class WindowStackTests
{
    private static UIContext Create() { var host = new TestHost(); return new UIContext(host, host, host); }
    private static async Task<T> Attach<T>(UINode parent) where T : UINode, new()
    { var node = new T(); await node.AttachAsync(parent, typeof(T).Name); return node; }
    private sealed class DerivedCounter : CounterView { }
    private sealed class OrderBehavior(List<int> order, int number) : NodeLifecycle
    {
        public override Task DisposeAsync(UINode node) { order.Add(number); return Task.CompletedTask; }
    }
    private sealed class GateTransition : IUITransition, IUITransitionRun
    {
        public TaskCompletionSource<bool> Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task PlayAsync(CancellationToken token) => Gate.Task.WaitAsync(token);
        public IUITransitionRun Create(TransitionContext context) => this; public void Restore() { }
    }
    private sealed class CleanupView : CounterView
    {
        public TaskCompletionSource<bool> Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task OnDisposeAsync() => Gate.Task;
    }

    [Fact] public void DetachedQueryPreservesTheOriginalContextFailure()
    {
        var panels = new PanelContainerNode();
        var failure = Assert.Throws<InvalidOperationException>(() => panels.TryGet<CounterView>(out _));
        Assert.Contains("get_Context", failure.StackTrace);
        Assert.DoesNotContain("ReportFailure", failure.StackTrace);
    }

    [Fact] public async Task SingleIdentityIsContainerLocalAndUsesRegistrationRatherThanRuntimeType()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new DerivedCounter(), "window");
        var first = await Attach<PanelContainerNode>(ui.Root); var second = await Attach<PanelContainerNode>(ui.Root);
        var a = await first.OpenAsync<CounterView>(); Assert.Same(a, await first.OpenAsync<CounterView>());
        Assert.NotSame(a, await second.OpenAsync<CounterView>());
        Assert.True(first.TryGet<CounterView>(out var found)); Assert.Same(a, found);
        Assert.False(first.TryGet<DerivedCounter>(out _));
        Assert.Equal(1, a.Initialized); Assert.Equal(1, a.Entered); Assert.Single(first.Children);
        Assert.Equal(2, ((TestHost)ui.Host).Loaded.Count);
    }

    [Fact] public async Task HideShowPreservesChildrenAndInitializationAndPublishesDisplayReasons()
    {
        await using var ui = Create(); var behavior = new RecordingLifecycle();
        ui.Register(() => { var n = new CounterView(); n.AddLifecycle(behavior); return n; }, "window");
        var panels = await Attach<PanelContainerNode>(ui.Root); var window = await panels.OpenAsync<CounterView>();
        var tabs = await Attach<TabContainerNode>(window); var page = new CounterView();
        await tabs.BindAsync("Page", page); await tabs.SwitchAsync("Page"); behavior.Events.Clear();
        await panels.HideAsync(window);
        Assert.False(window.IsVisible); Assert.False(page.IsActive); Assert.Same(panels, window.Parent);
        Assert.True(panels.TryGet<CounterView>(out var hidden)); Assert.Same(window, hidden);
        Assert.Equal(NodeState.Ready, window.State); Assert.Contains("exited:Hide", behavior.Events);
        Assert.Same(window, await panels.OpenAsync<CounterView>());
        Assert.True(page.IsActive); Assert.Equal(1, page.Entered); Assert.Equal(1, window.Initialized);
        Assert.Contains("entered:Show", behavior.Events); Assert.Single(((TestHost)ui.Host).Loaded);
        await panels.HideAsync(window); behavior.Events.Clear(); await panels.CloseAsync(window);
        Assert.Equal(new[] { "dispose" }, behavior.Events); Assert.Equal(NodeState.Disposed, page.State);
        Assert.False(panels.TryGet<CounterView>(out _));
        Assert.NotSame(window, await panels.OpenAsync<CounterView>());
    }

    [Fact] public async Task ParameterizedSingleReopensWithoutReplacingCreationParameters()
    {
        await using var ui = Create(); ui.Register<ArgumentView>(() => new ArgumentView()); var panels = await Attach<PanelContainerNode>(ui.Root);
        await Assert.ThrowsAsync<ArgumentException>(() => panels.OpenAsync<ArgumentView>());
        var window = await panels.OpenAsync<ArgumentView>("first");
        await Assert.ThrowsAsync<InvalidOperationException>(() => panels.OpenAsync<ArgumentView>("second"));
        Assert.Same(window, await panels.OpenAsync<ArgumentView>(args: null));
        await panels.HideAsync(window); Assert.Same(window, await panels.OpenAsync<ArgumentView>());
        Assert.Equal("first", window.Parameters);
    }

    [Fact] public async Task MultipleQueryRejectsAmbiguityAndRegistrationModeDoesNotAffectStacksOrModals()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView(), windowMode: WindowInstanceMode.Multiple);
        var panels = await Attach<PanelContainerNode>(ui.Root);
        var a = await panels.OpenAsync<CounterView>(); var b = await panels.OpenAsync<CounterView>();
        Assert.NotSame(a, b); await panels.HideAsync(a);
        Assert.Throws<InvalidOperationException>(() => panels.TryGet<CounterView>(out _));
        await panels.CloseAsync(b); Assert.True(panels.TryGet<CounterView>(out var remaining)); Assert.Same(a, remaining);
        ui.Register<ArgumentView>(() => new ArgumentView()); var stack = await Attach<StackContainerNode>(ui.Root);
        Assert.NotSame(await stack.PushAsync<ArgumentView>("a"), await stack.PushAsync<ArgumentView>("b"));
        var modals = await Attach<ModalContainerNode>(ui.Root);
        Assert.NotSame(await modals.OpenAsync<ArgumentView>("a"), await modals.OpenAsync<ArgumentView>("b"));
    }

    [Fact] public async Task WindowOperationsValidateLocalDisplayRatherThanAncestorVisibility()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); ui.Register<ArgumentView>(() => new ArgumentView());
        var outer = await Attach<PanelContainerNode>(ui.Root); var parent = await outer.OpenAsync<CounterView>();
        var inner = await Attach<PanelContainerNode>(parent); var child = await inner.OpenAsync<ArgumentView>("child");
        await outer.HideAsync(parent); Assert.False(child.IsVisible);
        await Assert.ThrowsAsync<InvalidOperationException>(() => inner.ShowAsync(child));
        await inner.HideAsync(child); await outer.ShowAsync(parent); Assert.False(child.IsVisible);
        await inner.ShowAsync(child); Assert.True(child.IsVisible);
        await Assert.ThrowsAsync<InvalidOperationException>(() => outer.HideAsync(child));
        await Assert.ThrowsAsync<InvalidOperationException>(() => inner.ShowAsync(child));
        await inner.CloseAsync(child);
        await Assert.ThrowsAsync<InvalidOperationException>(() => inner.HideAsync(child));
    }

    [Fact] public async Task WindowPrepareFailuresPreserveIdentityAndCommittedDisplay()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var panels = await Attach<PanelContainerNode>(ui.Root);
        var n = await panels.OpenAsync<CounterView>(); var failure = new IOException("fade");
        n.Transition = new FailureTransition(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => panels.HideAsync(n)));
        Assert.True(n.IsVisible); Assert.Same(n, await panels.OpenAsync<CounterView>());
        n.Transition = new InstantTransition(); await panels.HideAsync(n);
        n.Transition = new FailureTransition(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => panels.ShowAsync(n)));
        Assert.False(n.IsVisible); Assert.True(panels.TryGet<CounterView>(out var retained)); Assert.Same(n, retained);
        n.Transition = new InstantTransition(); await panels.ShowAsync(n);
    }

    [Fact] public async Task PendingWindowIsNotPublishedAndConcurrencyAndCancellationRemainObservable()
    {
        await using var ui = Create(); var gate = new GateTransition(); var n = new CounterView { Transition = gate };
        ui.Register(() => n); var panels = await Attach<PanelContainerNode>(ui.Root);
        using var cancellation = new CancellationTokenSource();
        var pending = panels.OpenAsync<CounterView>(cancellationToken: cancellation.Token);
        Assert.False(panels.TryGet<CounterView>(out _));
        await Assert.ThrowsAsync<InvalidOperationException>(() => panels.OpenAsync<CounterView>());
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(NodeState.Disposed, n.State); Assert.False(panels.TryGet<CounterView>(out _));
    }

    [Fact] public async Task HideAndShowCancellationRestoreLocalDisplay()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var panels = await Attach<PanelContainerNode>(ui.Root);
        var n = await panels.OpenAsync<CounterView>(); n.Transition = new GateTransition();
        using (var ct = new CancellationTokenSource())
        {
            var pending = panels.HideAsync(n, cancellationToken: ct.Token); ct.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); Assert.True(n.IsActive);
        }
        n.Transition = new InstantTransition(); await panels.HideAsync(n); n.Transition = new GateTransition();
        using (var ct = new CancellationTokenSource())
        {
            var pending = panels.ShowAsync(n, cancellationToken: ct.Token); ct.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); Assert.False(n.IsVisible);
        }
        n.Transition = new InstantTransition(); await panels.ShowAsync(n);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => panels.OpenAsync<CounterView>(cancellationToken: canceled.Token));
    }

    [Fact] public async Task CompletionFailureKeepsPublishedWindowAndHideShowCommits()
    {
        await using var ui = Create(); var behavior = new RecordingLifecycle(); var n = new CounterView(); n.AddLifecycle(behavior);
        var failure = new IOException("complete"); behavior.OnEntered = _ => throw failure;
        ui.Register(() => n); var panels = await Attach<PanelContainerNode>(ui.Root);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>()));
        Assert.True(panels.TryGet<CounterView>(out var found)); Assert.Same(n, found);
        behavior.OnEntered = null; behavior.OnExited = _ => throw failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => panels.HideAsync(n))); Assert.False(n.IsVisible);
        behavior.OnExited = null; behavior.OnEntered = _ => throw failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => panels.ShowAsync(n))); Assert.True(n.IsActive);
        behavior.OnEntered = null; await panels.CloseAsync(n);
    }

    [Fact] public async Task DirectDisposalRemovesSingleIndexAndHiddenCleanupFailureDoesNotLeaveOwnedEntry()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var panels = await Attach<PanelContainerNode>(ui.Root);
        var first = await panels.OpenAsync<CounterView>(); await first.DisposeAsync();
        Assert.False(panels.TryGet<CounterView>(out _)); var next = await panels.OpenAsync<CounterView>(); Assert.NotSame(first, next);
        await panels.HideAsync(next); var failure = new IOException("cleanup"); next.DisposeFailure = failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => panels.CloseAsync(next)));
        Assert.Equal(NodeState.Disposed, next.State); Assert.False(panels.TryGet<CounterView>(out _)); Assert.Empty(panels.Children);
    }

    [Fact] public async Task ContainerDisposalReleasesHiddenWindowResources()
    {
        var ui = Create(); ui.Register<CounterView>(() => new CounterView(), "window"); var panels = await Attach<PanelContainerNode>(ui.Root);
        var n = await panels.OpenAsync<CounterView>(); await panels.HideAsync(n); await ui.DisposeAsync();
        Assert.Equal(NodeState.Disposed, n.State); Assert.Equal(1, ((MemoryElement)n.Element!).Disposals);
        Assert.Empty(panels.Children);
    }

    [Fact] public async Task PopToSkipsIntermediateLifecyclesAndDisposesTopDownAfterCommit()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root);
        var disposal = new List<int>(); var behaviors = new List<RecordingLifecycle>();
        ui.Register(() => { var n = new CounterView(); var b = new RecordingLifecycle(); n.AddLifecycle(b); n.AddLifecycle(new OrderBehavior(disposal, behaviors.Count)); behaviors.Add(b); return n; });
        var a = await stack.PushAsync<CounterView>(); var b = await stack.PushAsync<CounterView>();
        var c = await stack.PushAsync<CounterView>(); var d = await stack.PushAsync<CounterView>();
        for (var i = 0; i < behaviors.Count; i++) { behaviors[i].Events.Clear(); behaviors[i].DisposeFailure = null; }
        behaviors[1].OnEntered = _ => throw new Exception("must not resume B");
        behaviors[0].OnEntered = context => { Assert.Same(a, stack.Current); Assert.Single(stack.History); Assert.True(a.IsInteractive); };
        await stack.PopToAsync(a);
        Assert.Equal(2, a.Entered); Assert.Equal(1, b.Entered); Assert.Equal(1, c.Entered);
        Assert.Equal(new[] { "dispose" }, behaviors[1].Events); Assert.Equal(new[] { "dispose" }, behaviors[2].Events);
        Assert.Contains("entered:Return", behaviors[0].Events); Assert.Contains("exited:Close", behaviors[3].Events);
        Assert.Equal(new[] { 3, 2, 1 }, disposal); Assert.All(new[] { b, c, d }, n => Assert.Equal(NodeState.Disposed, n.State));
    }

    [Fact] public async Task PopToRejectsCurrentForeignMissingAndDisposedTargets()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var stack = await Attach<StackContainerNode>(ui.Root);
        var a = await stack.PushAsync<CounterView>(); var b = await stack.PushAsync<CounterView>();
        var foreign = await Attach<CounterView>(ui.Root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PopToAsync(b));
        await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PopToAsync(foreign));
        await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PopToAsync(new UINode()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => stack.PopToAsync(null!));
        await a.DisposeAsync(); await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PopToAsync(a));
        Assert.Same(b, stack.Current); await stack.PopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PopToAsync(b));
    }

    [Fact] public async Task PopToKeepsEarlierHistoryWhenTargetIsNotTheRootPage()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var stack = await Attach<StackContainerNode>(ui.Root);
        var a = await stack.PushAsync<CounterView>(); var b = await stack.PushAsync<CounterView>();
        var c = await stack.PushAsync<CounterView>(); var d = await stack.PushAsync<CounterView>();
        await stack.PopToAsync(b);
        Assert.Equal(new[] { a, b }, stack.History); Assert.Same(b, stack.Current);
        Assert.False(a.IsVisible); Assert.Equal(1, a.Entered); Assert.Equal(NodeState.Ready, a.State);
        Assert.True(b.IsActive); Assert.Equal(2, b.Entered);
        Assert.All(new[] { c, d }, n => Assert.Equal(NodeState.Disposed, n.State));
        await stack.PopAsync(); Assert.Same(a, stack.Current); Assert.True(a.IsActive);
    }

    [Fact] public async Task FailedPopToPreparationRestoresEntireHistoryWithoutIntermediateCleanup()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var stack = await Attach<StackContainerNode>(ui.Root);
        var a = await stack.PushAsync<CounterView>(); var b = await stack.PushAsync<CounterView>(); var c = await stack.PushAsync<CounterView>();
        var failure = new IOException("return target"); a.EnterFailure = failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => stack.PopToAsync(a)));
        Assert.Equal(new[] { a, b, c }, stack.History); Assert.True(c.IsActive); Assert.False(a.IsVisible);
        Assert.Equal(NodeState.Ready, b.State); a.EnterFailure = null;
        using var ct = new CancellationTokenSource(); a.Transition = new GateTransition();
        var pending = stack.PopToAsync(a, cancellationToken: ct.Token); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(new[] { a, b, c }, stack.History); Assert.True(c.IsActive); a.Transition = new InstantTransition();
        await stack.PopToAsync(a); Assert.Same(a, stack.Current);
    }

    [Fact] public async Task ReturnCompletionAndMultipleCleanupFailuresPreserveOriginalAndReleaseEveryPage()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var stack = await Attach<StackContainerNode>(ui.Root);
        var a = await stack.PushAsync<CounterView>(); var b = await stack.PushAsync<CounterView>(); var c = await stack.PushAsync<CounterView>();
        var original = new IOException("completion");
        var sourceBehavior = new RecordingLifecycle { OnExited = _ => throw original };
        ui.Register(() => { var node = new ArgumentView(); node.AddLifecycle(sourceBehavior); return node; });
        var d = await stack.PushAsync<ArgumentView>("source");
        var cleanup1 = new Exception("cleanup c"); var cleanup2 = new Exception("cleanup b"); c.DisposeFailure = cleanup1; b.DisposeFailure = cleanup2;
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => stack.PopToAsync(a)));
        Assert.Same(a, stack.Current); Assert.True(a.IsActive); Assert.Single(stack.History);
        Assert.All(new UINode[] { b, c, d }, n => Assert.Equal(NodeState.Disposed, n.State));
        Assert.Contains(cleanup1, UIFailure.GetSecondaryExceptions(original)); Assert.Contains(cleanup2, UIFailure.GetSecondaryExceptions(original));
    }

    [Fact] public async Task PopToWaitsForCleanupEvenWhenCallerCancelsAfterCommit()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); ui.Register<CleanupView>(() => new CleanupView());
        var stack = await Attach<StackContainerNode>(ui.Root); var a = await stack.PushAsync<CounterView>();
        var cleanup = await stack.PushAsync<CleanupView>(); using var ct = new CancellationTokenSource();
        var pending = stack.PopToAsync(a, cancellationToken: ct.Token); Assert.Same(a, stack.Current); Assert.False(pending.IsCompleted);
        ct.Cancel(); cleanup.Gate.SetResult(true); await pending;
        Assert.Equal(NodeState.Disposed, cleanup.State); Assert.True(a.IsActive);
    }
}
