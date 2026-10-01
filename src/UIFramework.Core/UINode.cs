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
    private UIContext? _context;

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public UINode? Parent { get; private set; }
    public IReadOnlyList<UINode> Children => _children.AsReadOnly();
    public UIContext Context => _context ?? throw new InvalidOperationException("Node is not attached to a UI context.");
    public IUIElement? Element { get; private set; }
    public NodeState State { get; private set; }
    public bool IsVisible => _visible && (Parent?.IsVisible ?? true);
    public bool IsInteractive => IsVisible && _interactive && Context.AllowsInput(this) && (Parent?.IsInteractive ?? true);
    public CancellationToken Lifetime => _lifetime.Token;
    public IUITransition Transition { get; set; } = new InstantTransition();

    protected virtual bool InitiallyVisible => false;
    protected virtual Task OnInitializeAsync(CancellationToken token) => Task.CompletedTask;
    protected virtual Task OnEnterAsync(CancellationToken token) => Task.CompletedTask;
    protected virtual Task OnExitAsync(CancellationToken token) => Task.CompletedTask;
    protected virtual Task OnDisposeAsync() => Task.CompletedTask;
    protected virtual void OnChildRemoved(UINode child) { }
    internal virtual void SetParameters(object? args)
    {
        if (args != null) throw new ArgumentException($"{GetType().Name} does not accept parameters.", nameof(args));
    }

    internal void InitializeRoot(UIContext context)
    {
        _context = context; Name = "Root"; State = NodeState.Ready;
        _visible = _interactive = true;
    }

    /// <summary>Bind an existing display or attach a logical node. Does not load or clone a resource.</summary>
    public async Task AttachAsync(UINode parent, string name, IUIElement? element = null, CancellationToken cancellationToken = default)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (State != NodeState.Detached)
        {
            var ex = new InvalidOperationException("Node is already attached or disposed.");
            ReportFailure(ex, "Attach existing node");
            throw ex;
        }
        try
        {
            if (parent.State != NodeState.Ready && parent.State != NodeState.Initializing)
                throw new InvalidOperationException("Parent is not attached to an available UI context.");
            _context = parent.Context;
            Context.Host.VerifyAccess();
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
                token.ThrowIfCancellationRequested();
                State = NodeState.Ready;
                SetLocalState(InitiallyVisible, InitiallyVisible);
            }, cancellationToken, allowInitializing: true);
        }
        catch (Exception ex)
        {
            ReportFailure(ex, $"Attach {GetType().Name}/{name} ({Id})");
            // Only clean an attachment established by this call, never an existing node on an invalid reattach.
            if (State == NodeState.Initializing) await Context.CleanupAsync(ex, async () => await DisposeAsync());
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

    internal Task EnterAsync(CancellationToken token) => RunAsync("Enter", async ct =>
    {
        await OnEnterAsync(ct);
        ct.ThrowIfCancellationRequested();
        SetLocalState(true, false);
        await Transition.PlayAsync(Element, true, ct);
        ct.ThrowIfCancellationRequested();
        SetLocalState(true, true);
    }, token);

    internal Task ExitAsync(CancellationToken token) => RunAsync("Exit", async ct =>
    {
        SetLocalState(_visible, false);
        await OnExitAsync(ct);
        await Transition.PlayAsync(Element, false, ct);
        ct.ThrowIfCancellationRequested();
        SetLocalState(false, false);
    }, token);

    internal void SetLocalState(bool visible, bool interactive)
    {
        _visible = visible; _interactive = interactive; ApplyState();
    }
    internal void ApplyState()
    {
        Element?.SetVisible(IsVisible);
        Element?.SetInteractive(IsInteractive);
        foreach (var child in _children) child.ApplyState();
    }

    protected async Task RunAsync(string operation, Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default, bool allowInitializing = false)
    {
        var acquired = false;
        var previousFrame = Executing.Value;
        try
        {
            Context.Host.VerifyAccess();
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
            linked.Token.ThrowIfCancellationRequested();
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
        await Attempt(() => { SetLocalState(false, false); return Task.CompletedTask; });
        foreach (var child in Enumerable.Reverse(_children.ToArray()))
            await Attempt(async () => await child.DisposeAsync());
        await Attempt(async () =>
        {
            var previous = Executing.Value;
            Executing.Value = new OperationFrame(this, previous);
            try { await OnDisposeAsync(); }
            finally { Executing.Value = previous; }
        });
        if (Element != null) await Attempt(async () => await Element.DisposeAsync());
        await Attempt(() =>
        {
            if (Parent != null)
            {
                var parent = Parent; parent._children.Remove(this); Parent = null; parent.OnChildRemoved(this);
            }
            return Task.CompletedTask;
        });
        State = NodeState.Disposed;
        _lifetime.Dispose();
        if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
    }

    private void ReportFailure(Exception failure, string context)
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
