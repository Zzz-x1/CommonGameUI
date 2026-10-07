using System.Collections.ObjectModel;
using UIFramework.Mvvm;
using Xunit;

namespace UIFramework.Mvvm.Tests;

[ViewModel]
public class OwnedVm : IDisposable
{
    [FieldNotify] public int Gold { get; set; }
    [FieldNotify] public OwnedVm? Other { get; set; }
    [FieldNotify] public ObservableCollection<OwnedVm> Items { get; set; } = new();
    public int Disposals { get; private set; }
    public TaskCompletionSource Started { get; } = new();
    public TaskCompletionSource Finish { get; } = new();
    public Exception Error { get; } = new IOException("command identity");
    [Command] public async Task Wait(CancellationToken token)
    { Started.SetResult(); await Finish.Task.WaitAsync(token); }
    [Command] public Task Fail() => Task.FromException(Error);
    public void Dispose() => Disposals++;
}

[ViewModel]
public class ClosingVm : IDisposable, IAsyncDisposable
{
    public Func<CancellationToken, Task> Action { get; set; } = null!;
    public int Disposals { get; private set; }
    public Exception? ReleaseError { get; set; }
    public Func<Task> Release { get; set; } = () => Task.CompletedTask;
    public async ValueTask DisposeAsync() { await Release(); Dispose(); }
    [Command] public Task Close(CancellationToken token) => Action(token);
    [Command] public Task Other(CancellationToken token) => OtherAction(token);
    public Func<CancellationToken, Task> OtherAction { get; set; } = null!;
    public void Dispose() { Disposals++; if (ReleaseError != null) throw ReleaseError; }
}

public class RuntimeTests
{
    private sealed class Host : IUIHost
    { public void VerifyAccess() { } public object? CaptureFocus() => null; public void Focus(IUIElement? e) { } public void RestoreFocus(object? e) { } }
    private sealed class Loader : IUIResourceLoader
    { public Task<IUIResource> LoadAsync(string path, CancellationToken token) => throw new NotSupportedException(); }
    private sealed class Log : IUILog
    { public readonly List<Exception> Errors = new(); public void Error(Exception error, string context) => Errors.Add(error); }
    private sealed class Config(params ViewModelSource[] sources) : IViewBindingConfiguration
    { public IReadOnlyList<ViewModelSource> Sources => sources; public void Apply(UINode node) { } }
    private sealed class Assets : IViewModelAssets
    {
        public bool CanCreate(Type type) => false;
        public object CreateInstance(Type type) => throw new NotSupportedException();
        public readonly List<object> Released = new();
        public bool IsAsset(object value) => value is OwnedVm;
        public object Clone(object template) => new OwnedVm { Gold = ((OwnedVm)template).Gold };
        public void Remap(object template, object instance, Func<object, object> resolve)
        { var source = (OwnedVm)template; var clone = (OwnedVm)instance; clone.Other = source.Other == null ? null : (OwnedVm)resolve(source.Other); clone.Items = new(source.Items.Select(item => (OwnedVm)resolve(item))); }
        public Exception? ReleaseError;
        public void Release(object value) { Released.Add(value); if (ReleaseError != null) throw ReleaseError; }
    }
    private sealed class Click : ICommandTarget
    { public bool Enabled; public event Action? Invoked; public void SetEnabled(bool value) => Enabled = value; public void Press() => Invoked!(); public void Dispose() { } }
    private sealed class ListTarget : ICollectionTarget
    {
        public readonly List<object> Items = new(); public int Moves; public int Disposals;
        public void Insert(int index, object value) => Items.Insert(index == int.MaxValue ? Items.Count : index, value);
        public void Remove(int index) => Items.RemoveAt(index);
        public void Move(int oldIndex, int newIndex) { var value = Items[oldIndex]; Items.RemoveAt(oldIndex); Items.Insert(newIndex, value); Moves++; }
        public void Clear() => Items.Clear(); public void Dispose() => Disposals++;
    }
    private static UIContext Context(MvvmRuntime runtime, Config? config = null, Assets? assets = null)
    { var ui = new UIContext(new Host(), new Loader(), new Log()); ui.ConfigureMvvm(runtime, _ => config, assets); return ui; }
    private static ViewModelSource Source(string name, ViewModelCreation mode, object? template = null) => new() { Name = name, Type = typeof(OwnedVm), Creation = mode, Template = template };
    private static async Task<UINode> Node(UIContext ui, string name) { var node = new UINode(); await node.AttachAsync(ui.Root, name); return node; }

    [Fact] public async Task CommandMayCloseItsCachedWindowWithoutDisposingTheModel()
    {
        await using var runtime = new MvvmRuntime(); await using var ui = Context(runtime);
        ui.Register(() => new UINode(), cacheMode: NodeCacheMode.KeepAlive);
        var windows = new PanelContainerNode(); await windows.AttachAsync(ui.Root, "Windows");
        var node = await windows.OpenAsync<UINode>(); var vm = node.CreateViewModel<ClosingVm>("Dialog");
        vm.Action = token => windows.CloseAsync(node, cancellationToken: token);
        var click = new Click(); node.BindCommand("Dialog.Close", click); click.Press();
        await runtime.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(node.IsCached); Assert.Equal(0, vm.Disposals);
        Assert.Same(node, await windows.OpenAsync<UINode>()); Assert.Same(vm, node.GetViewModel<ClosingVm>("Dialog"));
        click.Press(); await runtime.WhenIdleAsync(); await windows.ClearCacheAsync(); Assert.Equal(1, vm.Disposals);
    }

    [Fact] public async Task PrivateAssetGraphCopiesAndReleasesCyclesOnce()
    {
        await using var runtime = new MvvmRuntime(); var assets = new Assets(); var template = new OwnedVm { Gold = 100 }; template.Other = template; template.Items.Add(template);
        await using var ui = Context(runtime, new Config(Source("Player", ViewModelCreation.CreateInstance, template)), assets);
        var a = await Node(ui, "A"); var b = await Node(ui, "B"); var first = a.GetViewModel<OwnedVm>("Player"); var second = b.GetViewModel<OwnedVm>("Player");
        Assert.NotSame(first, second); Assert.Same(first, first.Other); Assert.Same(first, first.Items[0]); Assert.NotSame(template.Items, first.Items);
        first.Gold = 30; first.Items.Clear(); Assert.Equal(100, template.Gold); Assert.Single(template.Items);
        await a.DisposeAsync(); Assert.Equal(1, first.Disposals); Assert.Equal(0, second.Disposals); Assert.Contains(first, assets.Released); Assert.Equal(0, template.Disposals);
    }
    [Fact] public async Task SharedAssetCrossesContextsAndLivesUntilRuntimeDisposal()
    {
        var runtime = new MvvmRuntime(); var assets = new Assets(); var template = new OwnedVm { Gold = 40 }; var config = new Config(Source("Player", ViewModelCreation.SharedAsset, template));
        await using var one = Context(runtime, config, assets); await using var two = Context(runtime, config, assets);
        var a = await Node(one, "A"); var b = await Node(two, "B"); var vm = a.GetViewModel<OwnedVm>("Player"); Assert.Same(vm, b.GetViewModel<OwnedVm>("Player"));
        await a.DisposeAsync(); Assert.Equal(0, vm.Disposals); await runtime.DisposeAsync(); Assert.Equal(1, vm.Disposals); Assert.Single(assets.Released); Assert.Equal(0, template.Disposals);
    }
    [Fact] public async Task FactoryOwnsCreatedVmButManualVmIsBorrowed()
    {
        await using var runtime = new MvvmRuntime(); var created = new OwnedVm(); runtime.RegisterFactory(() => created);
        await using var ui = Context(runtime, new Config(Source("Created", ViewModelCreation.CreateInstance)));
        var node = await Node(ui, "A"); var manual = new OwnedVm(); node.SetViewModel("Manual", manual); await node.DisposeAsync(); Assert.Equal(1, created.Disposals); Assert.Equal(0, manual.Disposals);
    }
    [Fact] public async Task CollectionMovePreservesIdentityAndReplacementUnsubscribes()
    {
        await using var runtime = new MvvmRuntime(); await using var ui = Context(runtime); var node = await Node(ui, "A"); var vm = new OwnedVm(); var a = new OwnedVm(); var b = new OwnedVm(); vm.Items.Add(a); vm.Items.Add(b); node.SetViewModel("Player", vm);
        var target = new ListTarget(); var binding = node.BindCollection("Player.Items", target); vm.Items.Move(0, 1); Assert.Equal(new[] { b, a }, target.Items); Assert.Equal(1, target.Moves);
        var old = vm.Items; vm.Items = new(); old.Add(new()); Assert.Empty(target.Items); vm.Items.Add(b); vm.Items[0] = a; Assert.Same(a, Assert.Single(target.Items)); vm.Items.Clear(); Assert.Empty(target.Items);
        binding.Dispose(); await node.DisposeAsync(); Assert.Equal(1, target.Disposals);
    }
    [Fact] public async Task SharedCommandDisablesAllButtonsUntilCompletion()
    {
        await using var runtime = new MvvmRuntime(); await using var ui = Context(runtime); var a = await Node(ui, "A"); var b = await Node(ui, "B"); var vm = new OwnedVm(); a.SetViewModel("Player", vm); b.SetViewModel("Player", vm);
        var first = new Click(); var second = new Click(); a.BindCommand("Player.Wait", first); b.BindCommand("Player.Wait", second); first.Press(); await vm.Started.Task;
        Assert.False(first.Enabled); Assert.False(second.Enabled); vm.Finish.SetResult(); await Task.Yield(); runtime.ObserveTasks(); Assert.True(first.Enabled); Assert.True(second.Enabled);
    }
    [Fact] public async Task AsyncFailureKeepsIdentityInObserverAndCleanup()
    {
        var log = new Log(); var runtime = new MvvmRuntime(log); var ui = Context(runtime); var node = await Node(ui, "A"); var vm = new OwnedVm(); node.SetViewModel("Player", vm); var button = new Click(); node.BindCommand("Player.Fail", button); button.Press();
        Assert.Same(vm.Error, Assert.Throws<IOException>(() => runtime.ObserveTasks())); Assert.Same(vm.Error, Assert.Single(log.Errors));
        Assert.Same(vm.Error, await Assert.ThrowsAsync<IOException>(() => node.DisposeAsync().AsTask())); await runtime.DisposeAsync(); await ui.DisposeAsync();
    }
    [Fact] public async Task ScopeDisposalCancelsAndAwaitsItsCommand()
    {
        var runtime = new MvvmRuntime(new Log()); var ui = Context(runtime); var node = await Node(ui, "A"); var vm = new OwnedVm(); node.SetViewModel("Player", vm); var button = new Click(); node.BindCommand("Player.Wait", button); button.Press(); await vm.Started.Task;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => node.DisposeAsync().AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.DisposeAsync().AsTask()); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ui.DisposeAsync().AsTask());
    }
    [Theory]
    [InlineData("node", false)] [InlineData("node", true)]
    [InlineData("ancestor", false)] [InlineData("context", true)]
    [InlineData("runtime", false)] [InlineData("runtime", true)]
    public async Task CommandCanAwaitClosureAndVmLivesUntilCommandFinishes(string owner, bool yieldFirst)
    {
        var runtime = new MvvmRuntime(new Log()); var ui = Context(runtime);
        var parent = await Node(ui, "Parent"); var node = new UINode(); await node.AttachAsync(parent, "Window");
        var vm = node.CreateViewModel<ClosingVm>("Dialog");
        var closed = new TaskCompletionSource(); var finish = new TaskCompletionSource();
        vm.Action = async token =>
        {
            if (yieldFirst) await Task.Yield();
            if (owner == "node") await node.DisposeAsync();
            else if (owner == "ancestor") await parent.DisposeAsync();
            else if (owner == "context") await ui.DisposeAsync();
            else await runtime.DisposeAsync();
            Assert.Equal(0, vm.Disposals); Assert.False(token.IsCancellationRequested);
            closed.SetResult(); await finish.Task;
        };
        var button = new Click(); node.BindCommand("Dialog.Close", button); button.Press();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, vm.Disposals);
        var idle = runtime.WhenIdleAsync(); Assert.False(idle.IsCompleted);
        var disposal = runtime.DisposeAsync().AsTask(); Assert.False(disposal.IsCompleted);
        finish.SetResult(); await disposal.WaitAsync(TimeSpan.FromSeconds(3)); await idle;
        Assert.Equal(1, vm.Disposals); Assert.True(runtime.IsDisposed);
        await ui.DisposeAsync();
    }
    private sealed class QueuedCloseGate : IUITransition, IUITransitionRun
    {
        public readonly TaskCompletionSource Finish = new();
        public IUITransitionRun Create(TransitionContext context) => this;
        public Task PlayAsync(CancellationToken token) => Finish.Task.WaitAsync(token);
        public void Restore() { }
    }
    [Fact] public async Task CommandCanAwaitSelfCloseQueuedBehindAnotherWindowOperation()
    {
        await using var runtime = new MvvmRuntime(new Log()); await using var ui = Context(runtime);
        ui.Register(() => new UINode(), windowMode: WindowInstanceMode.Multiple);
        var panels = new PanelContainerNode(); await panels.AttachAsync(ui.Root, "Windows");
        var blocker = await panels.OpenAsync<UINode>(); var node = await panels.OpenAsync<UINode>();
        var gate = new QueuedCloseGate(); blocker.Transition = gate;
        var blocking = panels.HideAsync(blocker); var vm = node.CreateViewModel<ClosingVm>("Dialog");
        vm.Action = async token => { await panels.CloseAsync(node); Assert.False(token.IsCancellationRequested); Assert.Equal(0, vm.Disposals); };
        var button = new Click(); node.BindCommand("Dialog.Close", button); button.Press();
        Assert.Equal(NodeState.Ready, node.State); gate.Finish.SetResult(); await blocking;
        await runtime.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(NodeState.Disposed, node.State); Assert.Equal(1, vm.Disposals);
    }

    [Fact] public async Task ExternalCloseCancelsCommandAndWaitsBeforeReleasingVm()
    {
        var runtime = new MvvmRuntime(new Log()); var ui = Context(runtime); var node = await Node(ui, "Window");
        var vm = node.CreateViewModel<ClosingVm>("Dialog"); var cancelled = new TaskCompletionSource(); var finish = new TaskCompletionSource();
        vm.Action = async token =>
        {
            using var registration = token.Register(() => cancelled.SetResult());
            await cancelled.Task; Assert.Equal(0, vm.Disposals); await finish.Task;
        };
        var button = new Click(); node.BindCommand("Dialog.Close", button); button.Press();
        var close = node.DisposeAsync().AsTask(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(close.IsCompleted); Assert.Equal(0, vm.Disposals);
        finish.SetResult(); await close.WaitAsync(TimeSpan.FromSeconds(3)); Assert.Equal(1, vm.Disposals);
        await runtime.DisposeAsync(); await ui.DisposeAsync();
    }
    [Fact] public async Task SelfCloseStillCancelsOtherCommandsAndWaitsForThem()
    {
        await using var runtime = new MvvmRuntime(new Log()); await using var ui = Context(runtime); var node = await Node(ui, "Window");
        var vm = node.CreateViewModel<ClosingVm>("Dialog"); var cancelled = new TaskCompletionSource(); var finish = new TaskCompletionSource(); var closed = false;
        vm.OtherAction = async token => { using var registration = token.Register(() => cancelled.SetResult()); await finish.Task; };
        vm.Action = async token => { await node.DisposeAsync(); Assert.False(token.IsCancellationRequested); closed = true; };
        var other = new Click(); var close = new Click(); node.BindCommand("Dialog.Other", other); node.BindCommand("Dialog.Close", close);
        other.Press(); close.Press(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3)); Assert.False(closed); Assert.Equal(0, vm.Disposals);
        finish.SetResult(); await runtime.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(3)); Assert.True(closed); Assert.Equal(1, vm.Disposals);
    }
    [Fact] public async Task FailureAfterSelfCloseAndDeferredReleaseKeepsOriginalException()
    {
        var log = new Log(); var runtime = new MvvmRuntime(log); var ui = Context(runtime); var node = await Node(ui, "Window");
        var vm = node.CreateViewModel<ClosingVm>("Dialog"); var original = new IOException("after close"); var cleanup = new InvalidOperationException("VM release"); vm.ReleaseError = cleanup;
        vm.Action = async _ => { await node.DisposeAsync(); throw original; };
        var button = new Click(); node.BindCommand("Dialog.Close", button); button.Press();
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => runtime.WhenIdleAsync()));
        Assert.Equal(1, vm.Disposals); Assert.Contains(original, log.Errors);
        Assert.Contains(cleanup, Assert.IsType<List<Exception>>(original.Data[UIFailure.SecondaryExceptionsKey]));
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => runtime.DisposeAsync().AsTask())); Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => ui.DisposeAsync().AsTask()));
    }

    [Fact] public async Task IdleWaitsForDeferredAsyncReleaseEvenAfterCommandFailure()
    {
        var runtime = new MvvmRuntime(new Log()); var ui = Context(runtime); var node = await Node(ui, "Window");
        var vm = node.CreateViewModel<ClosingVm>("Dialog"); var releaseStarted = new TaskCompletionSource(); var releaseFinish = new TaskCompletionSource();
        var original = new IOException("command failed after closing");
        vm.Release = async () => { releaseStarted.SetResult(); await releaseFinish.Task; };
        vm.Action = async _ => { await node.DisposeAsync(); throw original; };
        var button = new Click(); node.BindCommand("Dialog.Close", button); button.Press();
        await releaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var idle = runtime.WhenIdleAsync(); Assert.False(idle.IsCompleted); Assert.Equal(0, vm.Disposals);
        releaseFinish.SetResult(); Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => idle)); Assert.Equal(1, vm.Disposals);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => runtime.DisposeAsync().AsTask())); Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => ui.DisposeAsync().AsTask()));
    }

    [Fact] public async Task SelfShutdownDeferredSharedReleaseFailureIsObserved()
    {
        var log = new Log(); var runtime = new MvvmRuntime(log); var ui = Context(runtime); var node = await Node(ui, "Window");
        var original = new IOException("shared asset release"); var assets = new Assets { ReleaseError = original };
        var shared = runtime.GetSharedViewModel(new OwnedVm(), assets); var vm = node.CreateViewModel<ClosingVm>("Dialog");
        vm.Action = _ => runtime.DisposeAsync().AsTask();
        var button = new Click(); node.BindCommand("Dialog.Close", button); button.Press();
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => runtime.WhenIdleAsync()));
        Assert.Equal(1, shared.Disposals); Assert.Equal(1, vm.Disposals); Assert.Contains(original, log.Errors);
        Assert.Same(original, Assert.Throws<IOException>(() => runtime.ObserveTasks()));
        runtime.ObserveTasks(); Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => ui.DisposeAsync().AsTask()));
    }

    [Fact] public async Task AncestorCancellationReachesItsCommandBeforeWaitingForChildren()
    {
        await using var runtime = new MvvmRuntime(new Log()); await using var ui = Context(runtime);
        var parent = await Node(ui, "Parent"); var child = new UINode(); await child.AttachAsync(parent, "Child");
        var parentVm = parent.CreateViewModel<ClosingVm>("Dialog"); var childVm = child.CreateViewModel<ClosingVm>("Dialog");
        var parentCancelled = new TaskCompletionSource();
        parentVm.Action = async token => { using var registration = token.Register(() => parentCancelled.SetResult()); await parentCancelled.Task; };
        childVm.Action = _ => parentCancelled.Task;
        var parentButton = new Click(); var childButton = new Click(); parent.BindCommand("Dialog.Close", parentButton); child.BindCommand("Dialog.Close", childButton);
        parentButton.Press(); childButton.Press(); await parent.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, parentVm.Disposals); Assert.Equal(1, childVm.Disposals);
    }

    [Fact] public async Task ContextAutomaticallyInitializesAndReleasesMvvm()
    {
        var ui = new UIContext(new Host(), new Loader());
        var vm = new OwnedVm(); ui.RegisterViewModelFactory(() => vm);
        var node = await Node(ui, "Window"); Assert.Same(vm, node.CreateViewModel<OwnedVm>("Player"));
        var runtime = ui.GetMvvm(); await ui.DisposeAsync(); Assert.Equal(1, vm.Disposals); Assert.True(runtime.IsDisposed);
    }
    [Fact] public async Task SharedRuntimeIsReleasedByLastContextWithoutManualDisposal()
    {
        var one = new UIContext(new Host(), new Loader()); var two = new UIContext(new Host(), new Loader());
        var retired = two.GetMvvm(); two.ShareMvvm(one); var runtime = one.GetMvvm();
        Assert.Same(runtime, two.GetMvvm()); Assert.True(retired.IsDisposed);
        await one.DisposeAsync(); Assert.False(runtime.IsDisposed);
        await two.DisposeAsync(); Assert.True(runtime.IsDisposed);
    }

}
