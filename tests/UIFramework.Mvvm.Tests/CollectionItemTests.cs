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
        public Task<IUINode> CreateAsync(CancellationToken token) => AttachAsync(Display, null, token);
    }
    private sealed class Target : CollectionTargetBase
    {
        private readonly IUINode _owner; public readonly Dictionary<object, TaskCompletionSource<bool>> Gates = new();
        public readonly List<IUINode> Ordered = new(); public readonly List<Context> Contexts = new(); public bool Load; public Exception? Error; public Exception? CancelError;
        public Target(IUINode owner) : base(owner) => _owner = owner;
        protected override CollectionItemContext CreateContext(object item, int index)
        { var context = new Context(_owner, item, index, new[] { new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" } }); Contexts.Add(context); return context; }
        protected override async Task<IUINode> CreateItemAsync(CollectionItemContext context, CancellationToken token)
        { if (CancelError != null) token.Register(() => throw CancelError); if (Error != null) throw Error; if (Load) return await context.LoadAsync("Delayed", cancellationToken: token); if (Gates.TryGetValue(context.Item, out var gate)) await gate.Task; return await ((Context)context).CreateAsync(token); }
        protected override void Place(IUINode node, int index) { Ordered.Remove(node); Ordered.Insert(index, node); }
        protected override void Detach(IUINode node) => Ordered.Remove(node);
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
