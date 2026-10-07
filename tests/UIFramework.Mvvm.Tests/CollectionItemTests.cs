using UIFramework.Mvvm;
using Xunit;

namespace UIFramework.Mvvm.Tests;
public sealed class CollectionItemTests
{
    private sealed class Host : IUIHost
    { public void VerifyAccess() { } public object? CaptureFocus() => null; public void Focus(IUIElement? element) { } public void RestoreFocus(object? value) { } }
    private sealed class Element : IUIElement
    { public int Releases; public bool Visible; public void SetVisible(bool value) => Visible = value; public void SetInteractive(bool value) { } public void BringToFront() { } public object CaptureOrder() => 0; public void RestoreOrder(object value) { } public ValueTask DisposeAsync() { Releases++; return default; } }
    private sealed class Resource : IUIResource
    { public int Releases; public readonly Element Element = new(); public IUIElement Instantiate(IUIElement? mount) => Element; public void Dispose() => Releases++; }
    private sealed class Loader : IUIResourceLoader
    { public readonly TaskCompletionSource<IUIResource> Loaded = new(); public Task<IUIResource> LoadAsync(string path, CancellationToken token) => Loaded.Task; }
    private sealed class Context : CollectionItemContext
    {
        public readonly Element Display = new();
        public Context(IUINode owner, object item, int index, IReadOnlyList<CollectionSourceMapping> sources) : base(owner, item, index, false, new Element(), sources, "Item" + index + Guid.NewGuid()) { }
        protected override IUINode ResolveNode(IUIElement element) => new UINode();
        public Task<IUINode> CreateAsync(CancellationToken token, Action<IUINode>? configure = null) => AttachAsync(Display, configure, token);
    }
    private sealed class Target : CollectionTargetBase
    {
        private readonly IUINode _owner; public readonly Dictionary<object, TaskCompletionSource<bool>> Gates = new();
        public readonly List<IUINode> Ordered = new(); public readonly List<Context> Contexts = new(); public bool Load; public Exception? Error; public Exception? CancelError;
        public readonly Dictionary<object, Exception> Failures = new();
        public Action<IUINode>? ConfigureItem;
        public Target(IUINode owner) : base(owner) => _owner = owner;
        protected override CollectionItemContext CreateContext(object item, int index)
        { var context = new Context(_owner, item, index, new[] { new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" } }); Contexts.Add(context); return context; }
        protected override async Task<IUINode> CreateItemAsync(CollectionItemContext context, CancellationToken token)
        { if (CancelError != null) token.Register(() => throw CancelError); if (Error != null) throw Error; if (Load) return await context.LoadAsync("Delayed", cancellationToken: token); if (Gates.TryGetValue(context.Item, out var gate)) await gate.Task; if (Failures.TryGetValue(context.Item, out var failure)) throw failure; return await ((Context)context).CreateAsync(token, ConfigureItem); }
        protected override void Place(IUINode node, int index) { Ordered.Remove(node); Ordered.Insert(index, node); }
        protected override void Detach(IUINode node) => Ordered.Remove(node);
    }
    private sealed class Window : UINode
    {
        public Action<Window> Initialize = _ => { };
        public bool EnterStarted;
        protected override Task OnInitializeAsync(CancellationToken token) { Initialize(this); return Task.CompletedTask; }
        protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token) { EnterStarted = true; return Task.CompletedTask; }
    }
    private sealed class Configuration(Action<UINode> apply) : IViewBindingConfiguration
    {
        public IReadOnlyList<ViewModelSource> Sources => Array.Empty<ViewModelSource>();
        public void Apply(UINode node) => apply(node);
    }
    private static async Task<T> Container<T>(UIContext ui) where T : ContainerNode, new()
    { var container = new T(); await container.AttachAsync(ui.Root, typeof(T).Name, new Element()); return container; }

    [Fact] public async Task CachedCollectionContinuesUpdatingAndKeepsItsVm()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var windows = await Container<PanelContainerNode>(ui); var vm = new CollectionVm();
        vm.Items.Add(new ParameterVm()); Target target = null!;
        var view = new Window { Initialize = node =>
        { node.SetViewModel("Player", vm); target = new Target(node); node.BindCollection("Player.Items", target); } };
        ui.Register(() => view, cacheMode: NodeCacheMode.KeepAlive);
        await windows.OpenAsync<Window>(); var first = target.Ordered[0]; await windows.CloseAsync(view);
        vm.Items.Add(new ParameterVm()); await ui.WhenMvvmIdleAsync();
        Assert.Equal(2, target.Ordered.Count); Assert.Same(first, target.Ordered[0]);
        Assert.All(target.Ordered, item => { Assert.True(item.IsCached); Assert.False(item.IsVisible); });
        Assert.Same(view, await windows.OpenAsync<Window>()); Assert.Same(vm, view.GetViewModel<CollectionVm>("Player"));
        Assert.All(target.Ordered, item => Assert.True(item.IsVisible));
        await windows.CloseAsync(view); await windows.ClearCacheAsync();
        Assert.All(target.Contexts, context => Assert.Equal(1, context.Display.Releases));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task InitialCollectionsDelayWindowDisplayForCodeAndConfigurationBindings(bool configured)
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var background = new TaskCompletionSource<bool>(); var ownBackground = new TaskCompletionSource<bool>();
        var vm = new CollectionVm(); var first = new ParameterVm(); vm.Items.Add(first); vm.Items.Add(new ParameterVm());
        var prepared = new TaskCompletionSource<bool>(); Target target = null!;
        TaskCompletionSource<bool>? added = null;
        var view = new Window { Initialize = node =>
        {
            node.SetViewModel("Player", vm); node.ObserveMvvmTask(ownBackground.Task);
            target = new Target(node); target.Gates[first] = prepared;
            if (!configured) node.BindCollection("Player.Items", target);
        } };
        if (configured) ui.ConfigureMvvm(configuration: node => ReferenceEquals(node, view)
            ? new Configuration(owner => owner.BindCollection("Player.Items", target)) : null);
        var windows = await Container<PanelContainerNode>(ui);
        var unrelated = new UINode(); await unrelated.AttachAsync(ui.Root, "Unrelated"); unrelated.ObserveMvvmTask(background.Task);
        ui.Register(() => view);
        var opening = windows.OpenAsync<Window>();
        try
        {
            Assert.False(opening.IsCompleted); Assert.Equal(NodeState.Initializing, view.State);
            Assert.False(view.IsVisible); Assert.False(view.EnterStarted);
            Assert.All(target.Contexts, context => Assert.False(context.Display.Visible));
            prepared.SetResult(true); Assert.Same(view, await opening.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(2, target.Ordered.Count); Assert.True(view.IsVisible); Assert.True(view.EnterStarted);
            Assert.All(target.Contexts, context => Assert.True(context.Display.Visible));
            Assert.False(background.Task.IsCompleted); Assert.False(ownBackground.Task.IsCompleted);

            var later = new ParameterVm(); added = new TaskCompletionSource<bool>(); target.Gates[later] = added;
            vm.Items.Add(later); Assert.True(view.IsInteractive); Assert.Equal(2, target.Ordered.Count);
            added.SetResult(true); background.SetResult(true); ownBackground.SetResult(true);
            await ui.WhenMvvmIdleAsync(); Assert.Equal(3, target.Ordered.Count);
        }
        finally { prepared.TrySetResult(true); added?.TrySetResult(true); background.TrySetResult(true); ownBackground.TrySetResult(true); }
    }

    [Fact] public async Task NestedInitialCollectionCompletesBeforeItsParentWindowDisplays()
    {
        await using var ui = new UIContext(new Host(), new Loader()); var windows = await Container<ModalContainerNode>(ui);
        var vm = new OwnedVm(); var row = new OwnedVm(); var leaf = new OwnedVm(); row.Items.Add(leaf); vm.Items.Add(row);
        var ready = new TaskCompletionSource<bool>(); Target nested = null!; Target outer = null!;
        var view = new Window { Initialize = node =>
        {
            node.SetViewModel("Player", vm);
            outer = new Target(node) { ConfigureItem = item =>
            {
                nested = new Target(item); nested.Gates[leaf] = ready; item.BindCollection("Item.Items", nested);
            } };
            node.BindCollection("Player.Items", outer);
        } };
        ui.Register(() => view); var opening = windows.OpenAsync<Window>();
        try
        {
            Assert.False(opening.IsCompleted); Assert.False(view.IsVisible); Assert.False(view.EnterStarted);
            Assert.Empty(outer.Ordered); Assert.Empty(nested.Ordered);
            ready.SetResult(true); await opening.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Single(outer.Ordered); Assert.Single(nested.Ordered);
            Assert.True(outer.Ordered[0].IsVisible); Assert.True(nested.Ordered[0].IsVisible);
        }
        finally { ready.TrySetResult(true); }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CancelingInitialCollectionDrainsIgnoredLoadAndNeverDisplaysWindow(bool disposeContainer)
    {
        var loader = new Loader(); await using var ui = new UIContext(new Host(), loader); var windows = await Container<StackContainerNode>(ui);
        var vm = new CollectionVm(); vm.Items.Add(new ParameterVm()); Target target = null!;
        var view = new Window { Initialize = node =>
        { node.SetViewModel("Player", vm); target = new Target(node) { Load = true }; node.BindCollection("Player.Items", target); } };
        ui.Register(() => view); using var cancellation = new CancellationTokenSource();
        var opening = windows.PushAsync<Window>(cancellationToken: cancellation.Token);
        Assert.False(opening.IsCompleted); Assert.False(view.IsVisible);
        var disposing = disposeContainer ? windows.DisposeAsync().AsTask() : Task.CompletedTask;
        if (!disposeContainer) cancellation.Cancel();
        Assert.False(opening.IsCompleted); Assert.False(view.EnterStarted);
        var resource = new Resource(); loader.Loaded.SetResult(resource);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening.WaitAsync(TimeSpan.FromSeconds(2)));
        await disposing.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(NodeState.Disposed, view.State); Assert.Empty(target.Ordered); Assert.Empty(windows.History);
        Assert.Equal(1, resource.Releases); Assert.Equal(0, resource.Element.Releases);
    }

    [Fact] public async Task InitialCollectionFailureCancelsOtherEntriesAndKeepsTheOriginalError()
    {
        var ui = new UIContext(new Host(), new Loader()); var windows = await Container<PanelContainerNode>(ui);
        var vm = new CollectionVm(); var failedItem = new ParameterVm(); var pendingItem = new ParameterVm();
        vm.Items.Add(failedItem); vm.Items.Add(pendingItem);
        var fail = new TaskCompletionSource<bool>(); var finish = new TaskCompletionSource<bool>();
        var error = new IOException("initial item"); Target target = null!;
        var view = new Window { Initialize = node =>
        {
            node.SetViewModel("Player", vm); target = new Target(node);
            target.Gates[failedItem] = fail; target.Gates[pendingItem] = finish; target.Failures[failedItem] = error;
            node.BindCollection("Player.Items", target);
        } };
        ui.Register(() => view); var opening = windows.OpenAsync<Window>();
        fail.SetResult(true); Assert.False(opening.IsCompleted); Assert.False(view.IsVisible);
        finish.SetResult(true);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => opening.WaitAsync(TimeSpan.FromSeconds(2))));
        Assert.False(view.EnterStarted); Assert.Equal(NodeState.Disposed, view.State); Assert.Empty(windows.Children);
        Assert.Empty(target.Ordered); Assert.Equal(1, target.Contexts.Sum(context => context.Display.Releases));
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => ui.DisposeAsync().AsTask()));
    }
    [Fact] public async Task PendingMoveKeepsIdentityAndInitializesSourcesBeforeDisplay()
    {
        var loader = new Loader(); await using var ui = new UIContext(new Host(), loader); var owner = new UINode(); await owner.AttachAsync(ui.Root, "Owner", initiallyVisible: true);
        var player = new ParameterVm(); owner.SetViewModel("Player", player); var a = new ParameterVm(); var b = new ParameterVm();
        await using var target = new Target(owner); target.Gates[a] = new();
        target.Insert(0, a); target.Insert(1, b); var retained = target.Ordered.Single(); target.Move(0, 1);
        target.Gates[a].SetResult(true); await ui.WhenMvvmIdleAsync();
        Assert.Same(retained, target.Ordered[0]); Assert.Same(a, target.Ordered[1].GetViewModel<ParameterVm>("Item"));
        Assert.Same(player, target.Ordered[1].GetViewModel<ParameterVm>("Player")); Assert.True(target.Ordered[1].IsVisible);
        target.Remove(0); await ui.WhenMvvmIdleAsync(); Assert.Equal(1, target.Contexts[1].Display.Releases);
    }
    [Fact] public async Task ResetAndRemovalDoNotPublishLateItems()
    {
        await using var ui = new UIContext(new Host(), new Loader()); var owner = new UINode(); await owner.AttachAsync(ui.Root, "Owner"); owner.SetViewModel("Player", new ParameterVm());
        await using var target = new Target(owner); var stale = new ParameterVm(); var latest = new ParameterVm(); target.Gates[stale] = new();
        target.Insert(0, stale); target.Clear(); target.Insert(0, latest); target.Gates[stale].SetResult(true); await ui.WhenMvvmIdleAsync();
        Assert.Single(target.Ordered); Assert.Same(latest, target.Ordered[0].GetViewModel<ParameterVm>("Item")); Assert.Equal(1, target.Contexts[0].Display.Releases);
    }
    [Fact] public async Task ThrowingCancellationCallbackDoesNotPreventItemCleanup()
    {
        await using var ui = new UIContext(new Host(), new Loader()); var owner = new UINode(); await owner.AttachAsync(ui.Root, "Owner"); owner.SetViewModel("Player", new ParameterVm());
        var target = new Target(owner) { CancelError = new InvalidOperationException("Cancel callback") }; var item = new ParameterVm(); target.Gates[item] = new(); target.Insert(0, item);
        var error = Assert.Throws<AggregateException>(() => target.Remove(0)); Assert.Same(target.CancelError, error.InnerException);
        target.Gates[item].SetResult(true); await ui.WhenMvvmIdleAsync(); Assert.Empty(target.Ordered); Assert.Equal(1, target.Contexts.Single().Display.Releases);
        await target.DisposeAsync(); Assert.Throws<ObjectDisposedException>(() => target.Insert(0, item));
    }
    [Fact] public async Task CreationFailureKeepsOriginalExceptionAndPublishesNoDisplay()
    {
        var ui = new UIContext(new Host(), new Loader()); var owner = new UINode(); await owner.AttachAsync(ui.Root, "Owner"); owner.SetViewModel("Player", new ParameterVm());
        var error = new InvalidOperationException("Factory failed"); var target = new Target(owner) { Error = error }; target.Insert(0, new ParameterVm());
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => ui.WhenMvvmIdleAsync())); Assert.Empty(target.Ordered);
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => target.DisposeAsync().AsTask()));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => ui.DisposeAsync().AsTask()));
    }
    [Fact] public async Task OwnerCloseWaitsForIgnoredLoadCancellationAndReleasesAcquisition()
    {
        var loader = new Loader(); await using var ui = new UIContext(new Host(), loader); var owner = new UINode(); await owner.AttachAsync(ui.Root, "Owner");
        var vm = new CollectionVm(); vm.Items.Add(new ParameterVm()); owner.SetViewModel("Player", vm);
        var target = new Target(owner) { Load = true }; owner.BindCollection("Player.Items", target);
        var closing = owner.DisposeAsync().AsTask(); Assert.False(closing.IsCompleted); var resource = new Resource(); loader.Loaded.SetResult(resource); await closing;
        Assert.Empty(target.Ordered); Assert.Equal(1, resource.Releases); Assert.Equal(0, resource.Element.Releases);
    }
}
[ViewModel] public class CollectionVm
{ [FieldNotify] public System.Collections.ObjectModel.ObservableCollection<ParameterVm> Items { get; set; } = new(); }
