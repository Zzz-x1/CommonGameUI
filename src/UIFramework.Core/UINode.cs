using System.Runtime.ExceptionServices;

namespace UIFramework;

public class UINode : IUINode
{
    internal sealed class OperationFrame
    {
        public OperationFrame(UINode node, OperationFrame? previous) { Node = node; Previous = previous; Request = ContainerOperations.ExecutingRequest; }
        public bool Active = true;
        public ContainerOperations.Request? Request { get; }
        public UINode Node { get; }
        public OperationFrame? Previous { get; }
    }
    private static readonly AsyncLocal<OperationFrame?> Executing = new();
    internal static OperationFrame? CurrentOperation => Executing.Value;
    internal virtual Task WaitForOperationsAsync() => Task.CompletedTask;
    private readonly List<UINode> _children = new();
    private readonly CancellationTokenSource _lifetime = new();
    private int _busy;
    private TaskCompletionSource<bool>? _idle;
    private Task? _disposeTask;
    private int _transitionParticipants;
    private TaskCompletionSource<bool>? _transitionsIdle;
    private bool _visible;
    private bool _interactive;
    private bool _committedVisible;
    private bool _inputLocked;
    internal bool PublishedVisible, PublishedActive;
    private readonly List<INodeLifecycle> _handlers = new();
    private readonly List<INodeLifecycle> _initializedHandlers = new();
    private readonly List<IUINodeExtension> _initializedExtensions = new();
    private UIContext? _context;

    public UINode() { Owner = this; }
    protected UINode(IUINode owner) { Owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
    public IUINode Owner { get; }
    UINode IUINode.Node => this;
    IUINode? IUINode.Parent => Parent?.Owner;
    internal IReadOnlyList<UINode> ChildrenCore => _children;

    public bool HasEntered { get; internal set; }
    internal Type? RegisteredType;
    internal NodeCacheMode CacheMode;
    internal bool Cached, CacheReuse;
    internal UINode? SuspendedInputOwner;
    internal PresentationSession? Presentation;
    public bool IsCached => Cached || (Parent?.IsCached ?? false);
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public UINode? Parent { get; private set; }
    public IReadOnlyList<IUINode> Children => _children.Select(n => n.Owner).ToArray();
    public UIContext Context => _context ?? throw new InvalidOperationException("Node is not attached to a UI context.");
    public IUIElement? Element { get; private set; }
    internal UIResourceLease? ResourceLease { get; set; }
    public NodeState State { get; private set; }
    public bool IsVisible => _visible && (Parent?.IsVisible ?? true);
    internal bool CommittedVisible => _committedVisible && (Parent?.CommittedVisible ?? true);
    public bool IsActive => CommittedVisible && _interactive && Context.AllowsInput(this) && (Parent?.IsActive ?? true);
    public bool IsInteractive => IsVisible && IsActive && !_inputLocked && (Parent?.IsInteractive ?? true);
    public CancellationToken Lifetime => _lifetime.Token;
    private int _transitionOrder;
    /// <summary>Higher values draw on top during paired transitions; equal values use the container's default.</summary>
    public int TransitionOrder
    {
        get => _transitionOrder;
        set { try { VerifyConfiguration(); _transitionOrder = value; }
            catch (Exception ex) { ReportFailure(ex, "Configure transition order"); throw; } }
    }
    public TransitionState? TransitionState { get; private set; }
    public bool IsTransitioning => TransitionState?.IsRunning == true;
    public TransitionPhase? CurrentTransitionPhase => IsTransitioning ? TransitionState!.Context.Phase : null;
    public float TransitionProgress => TransitionState?.Progress ?? 0;
    public event Action<TransitionState>? TransitionChanged;
    internal void PublishTransition(TransitionContext context, TransitionStage stage, float progress)
    {
        var state = new TransitionState(context, stage, progress);
        TransitionState = state;
        Context.Notify(() => TransitionChanged?.Invoke(state), $"Transition {Name} ({Id}) {stage}");
    }
    private IUITransition? _transition;
    /// <summary>Null explicitly inherits the managing container/context configuration.</summary>
    public IUITransition? Transition
    {
        get => _transition;
        set { try { VerifyConfiguration(); _transition = value; }
            catch (Exception ex) { ReportFailure(ex, "Configure transition"); throw; } }
    }
    private void VerifyConfiguration()
    {
        _context?.Host.VerifyAccess(); _context?.VerifyMutation();
        if (this is ContainerNode { HasPendingOperations: true })
            throw new InvalidOperationException("Cannot configure a busy container.");
    }
    public Func<CancellationToken, Task>? DismissRequested { get; set; }

    protected virtual bool InitiallyVisible => false;
    protected virtual Task OnInitializeAsync(CancellationToken token) => Task.CompletedTask;
    protected virtual Task OnEnteringAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    protected virtual void OnEntered(LifecycleContext context) { }
    protected virtual Task OnExitingAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    protected virtual void OnExited(LifecycleContext context) { }
    protected virtual void OnActivated(StateChangeContext context) { }
    protected virtual void OnDeactivated(StateChangeContext context) { }
    protected virtual void OnVisibilityChanged(StateChangeContext context) { }

    public void AddLifecycle(INodeLifecycle handler)
    {
        try
        {
            if (State != NodeState.Detached) throw new InvalidOperationException("Register lifecycle behaviors before attachment.");
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.Any(h => ReferenceEquals(h, handler))) throw new InvalidOperationException("Lifecycle behavior already registered.");
            _handlers.Add(handler);
        }
        catch (Exception ex) { ReportFailure(ex, "AddLifecycle"); throw; }
    }
    public void RemoveLifecycle(INodeLifecycle handler)
    {
        try
        {
            _context?.Host.VerifyAccess();
            _context?.VerifyMutation();
            if (_busy != 0 || this is ContainerNode { HasPendingOperations: true } || State == NodeState.Initializing || State == NodeState.Disposing || State == NodeState.Disposed)
                throw new InvalidOperationException("Cannot change lifecycle behaviors during an operation or after disposal.");
            var index = _handlers.FindIndex(h => ReferenceEquals(h, handler));
            if (index < 0) throw new InvalidOperationException("Lifecycle behavior is not registered.");
            _handlers.RemoveAt(index);
            // Initialized behaviors remain owned until disposal, including removed subscribers.
        }
        catch (Exception ex) { ReportFailure(ex, "RemoveLifecycle"); throw; }
    }
    protected virtual Task OnDisposeAsync() => Task.CompletedTask;
    protected virtual Task OnChildRemovedAsync(UINode child) => Task.CompletedTask;
    protected virtual void OnChildRemoved(UINode child) { }
    internal void SetParameters(object? args) => OnSetParameters(args);
    protected virtual void OnSetParameters(object? args)
    {
        if (args != null) throw new ArgumentException($"{GetType().Name} does not accept parameters.", nameof(args));
    }

    internal void InitializeRoot(UIContext context)
    {
        _context = context; Name = "Root"; State = NodeState.Ready;
        _visible = _committedVisible = _interactive = PublishedVisible = PublishedActive = true;
    }

    /// <summary>Bind an existing display or attach a logical node. Does not load or clone a resource.</summary>
    public Task AttachAsync(IUINode owner, string name, IUIElement? element = null, CancellationToken cancellationToken = default, Action? configure = null, bool? initiallyVisible = null) =>
        AttachCoreAsync(owner, name, element, cancellationToken, configure, initiallyVisible, false);
    internal Task AttachRetainedAsync(IUINode owner, string name, IUIElement element, CancellationToken token, bool initiallyVisible) =>
        AttachCoreAsync(owner, name, element, token, null, initiallyVisible, true);
    private async Task AttachCoreAsync(IUINode owner, string name, IUIElement? element, CancellationToken cancellationToken, Action? configure, bool? initiallyVisible, bool allowCached)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var parent = owner.Node;
        if (State != NodeState.Detached)
        {
            var failure = new InvalidOperationException("Node is already attached or disposed.");
            ReportFailure(failure, "Attach existing node");
            throw failure;
        }
        try
        {
            if (parent.IsCached && !allowCached) throw new InvalidOperationException("Cannot attach to a cached node.");
            if (parent.State != NodeState.Ready && parent.State != NodeState.Initializing)
                throw new InvalidOperationException("Parent is not attached to an available UI context.");
            _context = parent.Context;
            Context.Host.VerifyAccess();
            Context.VerifyMutation();
            cancellationToken.ThrowIfCancellationRequested();
            if (State != NodeState.Detached) throw new InvalidOperationException("Node is already attached or disposed.");
            if (parent.IsCached && !allowCached) throw new InvalidOperationException("Cannot attach to a cached node.");
            if (parent.State != NodeState.Ready && parent.State != NodeState.Initializing)
                throw new InvalidOperationException("Parent is not available.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A node name is required.", nameof(name));
            for (var p = parent; p != null; p = p.Parent)
                if (ReferenceEquals(p, this)) throw new InvalidOperationException("Cyclic hierarchy.");
            Parent = parent; Name = name; Element = element;
            parent._children.Add(this);
            State = NodeState.Initializing;
            SetLocalState(false, false);
            await RunAsync("Initialize", async token =>
            {
                configure?.Invoke();
                foreach (var extension in Context.NodeExtensions)
                {
                    _initializedExtensions.Add(extension);
                    extension.BeforeInitialize(this);
                }
                await OnInitializeAsync(token);
                foreach (var handler in _handlers)
                {
                    _initializedHandlers.Add(handler);
                    await handler.InitializeAsync(Owner, token);
                }
                foreach (var extension in _initializedExtensions) await extension.AfterInitializeAsync(this, token);
                token.ThrowIfCancellationRequested();
                State = NodeState.Ready;
                SetLocalState(initiallyVisible ?? InitiallyVisible, initiallyVisible ?? InitiallyVisible);
                Context.PublishChanges(new LifecycleContext(this, Parent as ContainerNode, LifecycleReason.Initialization));
            }, cancellationToken, allowInitializing: true, allowCached: allowCached);
        }
        catch (Exception ex)
        {
            ReportFailure(ex, $"Attach {GetType().Name}/{name} ({Id})");
            // Only clean an attachment established by this call, never an existing node on an invalid reattach.
            if (Parent == parent && State != NodeState.Disposed) await Context.CleanupAsync(ex, async () => await DisposeAsync());
            throw;
        }
    }

    public IUINode GetChild(string name)
    {
        try
        {
            Context.Host.VerifyAccess();
            var matches = _children.Where(x => x.Name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Expected one child named '{name}', found {matches.Length}.");
            return matches[0].Owner;
        }
        catch (Exception ex) { ReportFailure(ex, $"GetChild {Name}/{name}"); throw; }
    }

    internal void BeginTransition()
    {
        if (_transitionParticipants++ == 0) _transitionsIdle = new TaskCompletionSource<bool>();
    }
    internal void EndTransition()
    {
        if (--_transitionParticipants == 0) _transitionsIdle!.SetResult(true);
    }

    internal Task PrepareEnterAsync(LifecycleContext context, CancellationToken token) => RunAsync("Entering", async ct =>
    {
        await OnEnteringAsync(context, ct);
        foreach (var handler in _handlers) await handler.EnteringAsync(context, ct);
        ct.ThrowIfCancellationRequested();
    }, token);

    internal Task PrepareExitAsync(LifecycleContext context, CancellationToken token) => RunAsync("Exiting", async ct =>
    {
        await OnExitingAsync(context, ct);
        foreach (var handler in _handlers) await handler.ExitingAsync(context, ct);
        ct.ThrowIfCancellationRequested();
    }, token);

    internal void NotifyEntered(LifecycleContext context) => Context.Notify(() =>
    { OnEntered(context); foreach (var handler in _handlers) handler.Entered(context); }, $"Entered {GetType().Name}/{Name} ({Id})");
    internal void NotifyExited(LifecycleContext context) => Context.Notify(() =>
    { OnExited(context); foreach (var handler in _handlers) handler.Exited(context); }, $"Exited {GetType().Name}/{Name} ({Id})");
    internal void NotifyActive(StateChangeContext context) => Context.Notify(() =>
    {
        if (context.Current) { OnActivated(context); foreach (var h in _handlers) h.Activated(context); }
        else { OnDeactivated(context); foreach (var h in _handlers) h.Deactivated(context); }
    }, $"{(context.Current ? "Activated" : "Deactivated")} {GetType().Name}/{Name} ({Id})");
    internal void NotifyVisible(StateChangeContext context) => Context.Notify(() =>
    { OnVisibilityChanged(context); foreach (var h in _handlers) h.VisibilityChanged(context); }, $"VisibilityChanged {GetType().Name}/{Name} ({Id})");
    internal void SetInputLocked(bool locked) { _inputLocked = locked; ApplyState(); }

    internal void SetLocalState(bool visible, bool interactive)
    {
        _visible = _committedVisible = visible; _interactive = interactive; ApplyState();
    }
    internal void StageVisibility(bool visible) { _visible = visible; ApplyState(); }
    internal void RestoreLocalState(bool visible, bool active)
    {
        Exception? first = null;
        try { SetLocalState(visible, active); }
        catch (Exception ex) { Context.Report(ex, $"Restore state {Name} ({Id})"); first = ex; }
        if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
    }
    internal void ApplyState()
    {
        Element?.SetVisible(IsVisible);
        Element?.SetInteractive(IsInteractive);
        OnApplyState();
        foreach (var child in _children) child.ApplyState();
    }
    protected virtual void OnApplyState() { }

    protected async Task RunAsync(string operation, Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default, bool allowInitializing = false, bool allowCached = false)
    {
        var acquired = false;
        var previousFrame = Executing.Value;
        OperationFrame? frame = null;
        try
        {
            Context.Host.VerifyAccess();
            Context.VerifyMutation();
            if (IsCached && !allowCached) throw new InvalidOperationException("Cannot operate on a cached node.");
            if (State != NodeState.Ready && !(allowInitializing && State == NodeState.Initializing))
                throw new InvalidOperationException($"Node {Name} is {State}.");
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException($"Concurrent or reentrant operation on {Name}: {operation}.");
            acquired = true;
            Executing.Value = frame = new OperationFrame(this, previousFrame);
            _idle = new TaskCompletionSource<bool>();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Lifetime);
            await action(linked.Token);
        }
        catch (Exception ex) { ReportFailure(ex, $"{operation} {GetType().Name}/{Name} ({Id})"); throw; }
        finally
        {
            if (acquired) { frame!.Active = false; Executing.Value = previousFrame; Interlocked.Exchange(ref _busy, 0); _idle!.TrySetResult(true); }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_context == null)
        {
            var failure = new InvalidOperationException("Detached node has no lifetime owner.");
            ReportFailure(failure, "Dispose detached node"); throw failure;
        }
        Context.Host.VerifyAccess();
        Context.VerifyMutation();
        for (var frame = Executing.Value; frame != null; frame = frame.Previous)
            for (var node = frame.Node; node != null; node = node.Parent)
                if (frame.Active && ReferenceEquals(node, this))
                {
                    var failure = new InvalidOperationException("Cannot dispose an active operation's owner from its own lifecycle; await the operation from outside first.");
                    Context.Report(failure, $"Reentrant dispose {Name}");
                    throw failure;
                }
        if (_disposeTask != null) return new ValueTask(_disposeTask);
        if (State == NodeState.Detached) throw new InvalidOperationException("Detached node has no lifetime owner.");
        State = NodeState.Disposing;
        // Publish the shared task before cancellation can synchronously call back into disposal.
        var completion = new TaskCompletionSource<Task>();
        _disposeTask = completion.Task.Unwrap();
        completion.SetResult(DisposeCoreAsync());
        return new ValueTask(_disposeTask);
    }

    private async Task DisposeCoreAsync()
    {
        var previous = Executing.Value;
        var disposalFrame = new OperationFrame(this, previous);
        Executing.Value = disposalFrame;
        try
        {
            Exception? first = null;
            async Task Attempt(Func<Task> action)
            {
                try { await action(); }
                catch (Exception ex)
                {
                    Context.Report(ex, $"Dispose {GetType().Name}/{Name} ({Id})");
                    if (first == null) first = ex; else UIFailure.AddSecondary(first, ex);
                }
            }
            await Attempt(() => { if (this is IDialogResult dialog) dialog.EndSession(); _lifetime.Cancel(); return Task.CompletedTask; });
            if (_busy != 0 && _idle != null) await _idle.Task;
            await Attempt(WaitForOperationsAsync);
            if (_transitionParticipants != 0) await _transitionsIdle!.Task;
            await Attempt(() => { SetLocalState(false, false); Context.PublishChanges(new LifecycleContext(this, Parent as ContainerNode, LifecycleReason.Disposal), continueOnFailure: true); return Task.CompletedTask; });
            foreach (var child in Enumerable.Reverse(_children.ToArray()))
                await Attempt(async () => await child.DisposeAsync());
            foreach (var extension in _initializedExtensions.AsEnumerable().Reverse())
                await Attempt(async () => await extension.DisposeAsync(this));
            await Attempt(OnDisposeAsync);
            foreach (var handler in _initializedHandlers.AsEnumerable().Reverse())
                await Attempt(() => handler.DisposeAsync(Owner));
            if (Element != null) await Attempt(async () => await Element.DisposeAsync());
            await Attempt(() => { ResourceLease?.Dispose(); ResourceLease = null; return Task.CompletedTask; });
            var formerParent = Parent;
            await Attempt(() =>
            {
                if (Parent != null)
                {
                    var parent = Parent; parent._children.Remove(this); Parent = null; parent.OnChildRemoved(this);
                }
                return Task.CompletedTask;
            });
            if (formerParent != null) await Attempt(() => formerParent.OnChildRemovedAsync(this));
            Cached = CacheReuse = false; SuspendedInputOwner = null;
            State = NodeState.Disposed;
            _lifetime.Dispose();
            if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
        }
        finally { disposalFrame.Active = false; Executing.Value = previous; }
    }

    internal void ReportFailure(Exception failure, string context)
    {
        if (_context != null) _context.Report(failure, context);
        else new ConsoleUILog().Error(failure, context);
    }
}

public class UINode<TArgs> : UINode, IParameterizedNode<TArgs>
{
    public TArgs Parameters { get; private set; } = default!;
    protected override void OnSetParameters(object? args)
    {
        if (!(args is TArgs typed)) throw new ArgumentException($"Expected parameters of type {typeof(TArgs).FullName}.", nameof(args));
        Parameters = typed;
    }
}
