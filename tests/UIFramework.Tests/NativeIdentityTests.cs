using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public sealed class NativeIdentityTests
{
    private class View : IUINode
    {
        private UINode? _node;
        public UINode Node => _node ??= CreateNode();
        public bool Configured;
        public int Initialized;
        public object? Arguments;
        protected virtual UINode CreateNode() => new Backing(this);
        private sealed class Backing(View owner) : UINode(owner)
        {
            protected override void OnSetParameters(object? args) => owner.Arguments = args;
            protected override Task OnInitializeAsync(CancellationToken token)
            { Assert.True(owner.Configured); owner.Initialized++; return Task.CompletedTask; }
        }
    public Guid Id => Node.Id;
    public string Name => Node.Name;
    public UIContext Context => Node.Context;
    public IUINode? Parent => Node.Parent?.Owner;
    public IReadOnlyList<IUINode> Children => Node.Children;
    public IUIElement? Element => Node.Element;
    public NodeState State => Node.State;
    public bool HasEntered => Node.HasEntered;
    public bool IsVisible => Node.IsVisible;
    public bool IsActive => Node.IsActive;
    public bool IsInteractive => Node.IsInteractive;
    public CancellationToken Lifetime => Node.Lifetime;
    public int TransitionOrder { get => Node.TransitionOrder; set => Node.TransitionOrder = value; }
    public IUITransition? Transition { get => Node.Transition; set => Node.Transition = value; }
    public TransitionState? TransitionState => Node.TransitionState;
    public bool IsTransitioning => Node.IsTransitioning;
    public TransitionPhase? CurrentTransitionPhase => Node.CurrentTransitionPhase;
    public float TransitionProgress => Node.TransitionProgress;
    public Func<CancellationToken, Task>? DismissRequested { get => Node.DismissRequested; set => Node.DismissRequested = value; }
    public event Action<TransitionState>? TransitionChanged { add => Node.TransitionChanged += value; remove => Node.TransitionChanged -= value; }
    public IUINode GetChild(string name) => Node.GetChild(name);
    public void AddLifecycle(INodeLifecycle handler) => Node.AddLifecycle(handler);
    public void RemoveLifecycle(INodeLifecycle handler) => Node.RemoveLifecycle(handler);
    public ValueTask DisposeAsync() => Node.DisposeAsync();
    }
    private sealed class Dialog : View, IDialogNode<bool>
    {
        private sealed class Backing(Dialog owner) : DialogNode<bool>(owner) { }
        protected override UINode CreateNode() => new Backing(this);
        public void Complete(bool value) => ((DialogNode<bool>)Node).Complete(value);
        public Task<bool> WaitForResultAsync(CancellationToken token) => ((DialogNode<bool>)Node).WaitForResultAsync(token);
    }
    private sealed class Element(IUINode owner) : MemoryElement, IUIResourceNodeProvider
    {
        public IUINode GetNode(Type type) => type.IsInstanceOfType(owner) ? owner : throw new InvalidOperationException("Wrong native script.");
    }
    private sealed class Resource(Func<IUINode> create, List<Element> instances) : IUIResource
    {
        public int Released;
        public IUIElement Instantiate(IUIElement? mount)
        { var instance = new Element(create()); instances.Add(instance); return instance; }
        public void Dispose() => Released++;
    }
    private sealed class Loader(Func<IUINode> create) : IUIResourceLoader
    {
        public readonly List<Element> Instances = new();
        public readonly List<Resource> Resources = new();
        public Task<IUIResource> LoadAsync(string resource, CancellationToken token)
        { var value = new Resource(create, Instances); Resources.Add(value); return Task.FromResult<IUIResource>(value); }
    }
    private static async Task<T> Container<T>(UIContext ui) where T : ContainerNode, new()
    { var container = new T(); await container.AttachAsync(ui.Root, typeof(T).Name); return container; }

    [Fact] public async Task ResourceOwnersPreserveSingleIdentityAndCreationConfiguration()
    {
        var loader = new Loader(() => new View()); var host = new TestHost();
        await using var ui = new UIContext(host, loader, host);
        var configurations = 0;
        ui.Register<View>("native", view => { view.Configured = true; configurations++; });
        var windows = await Container<PanelContainerNode>(ui);
        var view = await windows.OpenAsync<View>("argument");
        Assert.Equal("argument", view.Arguments); Assert.Equal(1, view.Initialized);
        Assert.Same(view, windows.Children.Single()); Assert.Same(view, windows.GetChild(nameof(View)));
        Assert.Same(view, await windows.OpenAsync<View>());
        Assert.True(windows.TryGet<View>(out var found)); Assert.Same(view, found);
        await windows.HideAsync(view); Assert.False(view.IsVisible);
        Assert.Same(view, await windows.OpenAsync<View>());
        Assert.Equal(1, configurations);
        await windows.CloseAsync(view); Assert.Equal(NodeState.Disposed, view.State);
        var next = await windows.OpenAsync<View>(); Assert.NotSame(view, next); Assert.Equal(2, configurations);
    }

    [Fact] public async Task HistoryAndTransitionSourcesExposeBusinessOwners()
    {
        var loader = new Loader(() => new View()); var host = new TestHost();
        await using var ui = new UIContext(host, loader, host);
        ui.Register<View>("native", view => view.Configured = true);
        var stack = await Container<StackContainerNode>(ui);
        var first = await stack.PushAsync<View>();
        var contexts = new List<TransitionContext>();
        stack.ChildTransition = Transitions.Select(context => { contexts.Add(context); return new InstantTransition(); });
        var second = await stack.PushAsync<View>();
        Assert.Same(second, stack.Current); Assert.Equal(new IUINode[] { first, second }, stack.History);
        Assert.All(contexts, c => { Assert.Same(first, c.Source); Assert.Same(second, c.Target); Assert.IsType<View>(c.Node); });
        await stack.PopToAsync(first); Assert.Same(first, stack.Current); Assert.Equal(NodeState.Disposed, second.State);
    }

    [Fact] public async Task DialogReturnsItsResultAfterOwnedCleanup()
    {
        var loader = new Loader(() => new Dialog()); var host = new TestHost();
        await using var ui = new UIContext(host, loader, host); ui.Register<Dialog>("dialog");
        var modals = await Container<ModalContainerNode>(ui);
        var waiting = modals.ShowDialogAsync<Dialog, bool>();
        var dialog = Assert.IsType<Dialog>(modals.Current);
        Assert.Same(dialog, ui.InputOwner);
        dialog.Complete(true); Assert.True(await waiting); Assert.Equal(NodeState.Disposed, dialog.State);
        Assert.Null(ui.InputOwner);
    }

    [Fact] public async Task ConfigurationFailuresKeepOriginalExceptionAndReleaseResource()
    {
        var loader = new Loader(() => new View()); var host = new TestHost();
        await using var ui = new UIContext(host, loader, host);
        var original = new IOException("configuration");
        ui.Register<View>("native", _ => throw original);
        var windows = await Container<PanelContainerNode>(ui);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => windows.OpenAsync<View>()));
        Assert.Empty(windows.Children); Assert.Equal(1, loader.Instances.Single().Disposals);
        Assert.Equal(1, loader.Resources.Single().Released);
    }
    [Fact] public async Task NullFactoryDoesNotFallBackToAResourceScript()
    {
        var loader = new Loader(() => new View()); var host = new TestHost();
        await using var ui = new UIContext(host, loader, host);
        ui.Register<View>(() => null!, "native");
        var windows = await Container<PanelContainerNode>(ui);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => windows.OpenAsync<View>());
        Assert.Contains("factory returned null", error.Message);
        Assert.Empty(loader.Instances); Assert.Empty(windows.Children);
    }
    private sealed class CancelingTransition(Action restore) : IUITransition, IUITransitionRun
    {
        public readonly TaskCompletionSource<bool> Started = new();
        public IUITransitionRun Create(TransitionContext context) => this;
        public Task PlayAsync(CancellationToken token)
        { Started.SetResult(true); return Task.Delay(Timeout.Infinite, token); }
        public void Restore() => restore();
    }
    [Fact] public async Task DisposingParticipantWaitsForTransitionRestorationBeforeReleasingDisplay()
    {
        var loader = new Loader(() => new View()); var host = new TestHost();
        await using var ui = new UIContext(host, loader, host);
        ui.Register<View>("native", view => view.Configured = true);
        var windows = await Container<PanelContainerNode>(ui);
        var restored = 0;
        var transition = new CancelingTransition(() => { Assert.Equal(0, loader.Instances.Single().Disposals); restored++; });
        windows.ChildTransition = transition;
        var opening = windows.OpenAsync<View>(); await transition.Started.Task;
        var owner = loader.Instances.Single().GetNode(typeof(View));
        var disposal = owner.DisposeAsync().AsTask();
        var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await disposal;
        Assert.Equal(1, restored); Assert.Equal(1, loader.Instances.Single().Disposals);
        Assert.Empty(UIFailure.GetSecondaryExceptions(canceled));
    }
}
