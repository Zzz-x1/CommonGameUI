using System.Runtime.ExceptionServices;

namespace UIFramework;

public class UINode : IAsyncDisposable
{
    private sealed class OperationFrame
    {
        public OperationFrame(UINode node, OperationFrame? previous) { Node = node; Previous = previous; }
        public UINode Node { get; }
        public OperationFrame? Previous { get; }
    }
    private static readonly AsyncLocal<OperationFrame?> Executing = new();
    private readonly List<UINode> _children = new();
    private readonly CancellationTokenSource _lifetime = new();
    private int _busy;
    private TaskCompletionSource<bool>? _idle;
    private Task? _disposeTask;
    private bool _visible;
    private bool _interactive;
    private bool _committedVisible;
    private bool _inputLocked;
    internal bool PublishedVisible, PublishedActive;
    private readonly List<INodeLifecycle> _handlers = new();
    private readonly List<INodeLifecycle> _initializedHandlers = new();
    private UIContext? _context;

    public bool HasEntered { get; internal set; }
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public UINode? Parent { get; private set; }
    public IReadOnlyList<UINode> Children => _children.AsReadOnly();
    public UIContext Context => _context ?? throw new InvalidOperationException("Node is not attached to a UI context.");
    public IUIElement? Element { get; private set; }
    public NodeState State { get; private set; }
    public bool IsVisible => _visible && (Parent?.IsVisible ?? true);
    internal bool CommittedVisible => _committedVisible && (Parent?.CommittedVisible ?? true);
    public bool IsActive => CommittedVisible && _interactive && Context.AllowsInput(this) && (Parent?.IsActive ?? true);
    public bool IsInteractive => IsVisible && IsActive && !_inputLocked && (Parent?.IsInteractive ?? true);
    public CancellationToken Lifetime => _lifetime.Token;
    private IUITransition? _transition;
    /// <summary>Null explicitly inherits the managing container/context configuration.</summary>
    public IUITransition? Transition
    {
        get => _transition;
        set { try { _context?.Host.VerifyAccess(); _context?.VerifyMutation(); _transition = value; }
            catch (Exception ex) { ReportFailure(ex, "Configure transition"); throw; } }
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
            if (_busy != 0 || State == NodeState.Initializing || State == NodeState.Disposing || State == NodeState.Disposed)
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
    internal virtual void SetParameters(object? args)
    {
        if (args != null) throw new ArgumentException($"{GetType().Name} does not accept parameters.", nameof(args));
    }

    internal void InitializeRoot(UIContext context)
    {
        _context = context; Name = "Root"; State = NodeState.Ready;
        _visible = _committedVisible = _interactive = PublishedVisible = PublishedActive = true;
    }

    /// <summary>Bind an existing display or attach a logical node. Does not load or clone a resource.</summary>
    public async Task AttachAsync(UINode parent, string name, IUIElement? element = null, CancellationToken cancellationToken = default)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (State != NodeState.Detached)
        {
            var failure = new InvalidOperationException("Node is already attached or disposed.");
            ReportFailure(failure, "Attach existing node");
            throw failure;
        }
        try
        {
            if (parent.State != NodeState.Ready && parent.State != NodeState.Initializing)
                throw new InvalidOperationException("Parent is not attached to an available UI context.");
            _context = parent.Context;
            Context.Host.VerifyAccess();
            Context.VerifyMutation();
            cancellationToken.ThrowIfCancellationRequested();
            if (State != NodeState.Detached) throw new InvalidOperationException("Node is already attached or disposed.");
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
                await OnInitializeAsync(token);
                foreach (var handler in _handlers)
                {
                    _initializedHandlers.Add(handler);
                    await handler.InitializeAsync(this, token);
                }
                token.ThrowIfCancellationRequested();
                State = NodeState.Ready;
                SetLocalState(InitiallyVisible, InitiallyVisible);
                Context.PublishChanges(new LifecycleContext(this, Parent as ContainerNode, LifecycleReason.Initialization));
            }, cancellationToken, allowInitializing: true);
        }
        catch (Exception ex)
        {
            ReportFailure(ex, $"Attach {GetType().Name}/{name} ({Id})");
            // Only clean an attachment established by this call, never an existing node on an invalid reattach.
            if (Parent == parent && State != NodeState.Disposed) await Context.CleanupAsync(ex, async () => await DisposeAsync());
            throw;
        }
    }

    public UINode GetChild(string name)
    {
        try
        {
            Context.Host.VerifyAccess();
            var matches = _children.Where(x => x.Name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Expected one child named '{name}', found {matches.Length}.");
            return matches[0];
        }
        catch (Exception ex) { ReportFailure(ex, $"GetChild {Name}/{name}"); throw; }
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
        CancellationToken cancellationToken = default, bool allowInitializing = false)
    {
        var acquired = false;
        var previousFrame = Executing.Value;
        try
        {
            Context.Host.VerifyAccess();
            Context.VerifyMutation();
            if (State != NodeState.Ready && !(allowInitializing && State == NodeState.Initializing))
                throw new InvalidOperationException($"Node {Name} is {State}.");
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException($"Concurrent or reentrant operation on {Name}: {operation}.");
            acquired = true;
            Executing.Value = new OperationFrame(this, previousFrame);
            _idle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Lifetime);
            await action(linked.Token);
        }
        catch (Exception ex) { ReportFailure(ex, $"{operation} {GetType().Name}/{Name} ({Id})"); throw; }
        finally
        {
            if (acquired) { Executing.Value = previousFrame; Interlocked.Exchange(ref _busy, 0); _idle!.TrySetResult(true); }
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
                if (ReferenceEquals(node, this))
                {
                    var failure = new InvalidOperationException("Cannot dispose an active operation's owner from its own lifecycle; await the operation from outside first.");
                    Context.Report(failure, $"Reentrant dispose {Name}");
                    throw failure;
                }
        if (_disposeTask != null) return new ValueTask(_disposeTask);
        if (State == NodeState.Detached) throw new InvalidOperationException("Detached node has no lifetime owner.");
        State = NodeState.Disposing;
        _disposeTask = DisposeCoreAsync();
        return new ValueTask(_disposeTask);
    }

    private async Task DisposeCoreAsync()
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
        await Attempt(() => { _lifetime.Cancel(); return Task.CompletedTask; });
        if (_busy != 0 && _idle != null) await _idle.Task;
        await Attempt(() => { SetLocalState(false, false); Context.PublishChanges(new LifecycleContext(this, Parent as ContainerNode, LifecycleReason.Disposal), continueOnFailure: true); return Task.CompletedTask; });
        foreach (var child in Enumerable.Reverse(_children.ToArray()))
            await Attempt(async () => await child.DisposeAsync());
        await Attempt(async () =>
        {
            var previous = Executing.Value;
            Executing.Value = new OperationFrame(this, previous);
            try { await OnDisposeAsync(); }
            finally { Executing.Value = previous; }
        });
        foreach (var handler in _initializedHandlers.AsEnumerable().Reverse())
            await Attempt(async () =>
            {
                var previous = Executing.Value;
                Executing.Value = new OperationFrame(this, previous);
                try { await handler.DisposeAsync(this); }
                finally { Executing.Value = previous; }
            });
        if (Element != null) await Attempt(async () => await Element.DisposeAsync());
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
        State = NodeState.Disposed;
        _lifetime.Dispose();
        if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
    }

    internal void ReportFailure(Exception failure, string context)
    {
        if (_context != null) _context.Report(failure, context);
        else new ConsoleUILog().Error(failure, context);
    }
}

public class UINode<TArgs> : UINode
{
    public TArgs Parameters { get; private set; } = default!;
    internal override void SetParameters(object? args)
    {
        if (!(args is TArgs typed)) throw new ArgumentException($"Expected parameters of type {typeof(TArgs).FullName}.", nameof(args));
        Parameters = typed;
    }
}
