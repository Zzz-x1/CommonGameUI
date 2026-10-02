namespace UIFramework;

public sealed class UIContext : IAsyncDisposable
{
    private sealed class Registration
    {
        public Registration(Func<UINode> factory, string? resource, WindowInstanceMode windowMode)
        { Factory = factory; Resource = resource; WindowMode = windowMode; }
        public Func<UINode> Factory { get; }
        public string? Resource { get; }
        public WindowInstanceMode WindowMode { get; }
    }
    private readonly Dictionary<Type, Registration> _registrations = new();
    private readonly UIResources _resources;
    private Task? _disposeTask;

    public UIContext(IUIHost host, IUIResourceLoader loader, IUILog? log = null)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Loader = loader ?? throw new ArgumentNullException(nameof(loader));
        Log = log ?? new ConsoleUILog();
        Host.VerifyAccess();
        Root = new UINode();
        Root.InitializeRoot(this);
        _resources = new UIResources(this);
    }

    public UINode Root { get; }
    public IUIHost Host { get; }
    public IUIResourceLoader Loader { get; }
    public IUILog Log { get; }
    public UINode? InputOwner { get; private set; }
    private IUITransition _defaultTransition = new InstantTransition();
    public IUITransition DefaultTransition
    {
        get => _defaultTransition;
        set
        {
            try { Host.VerifyAccess(); VerifyMutation(); _defaultTransition = value ?? throw new ArgumentNullException(nameof(value)); }
            catch (Exception ex) { Report(ex, "Configure default transition"); throw; }
        }
    }

    private int _notificationDepth;
    internal void VerifyMutation()
    {
        if (_notificationDepth != 0) throw new InvalidOperationException("Read-only callbacks allow queries, not framework mutations.");
    }
    internal T Query<T>(Func<T> query, string stage)
    {
        _notificationDepth++;
        try { return query(); }
        catch (Exception ex) { Report(ex, stage); throw; }
        finally { _notificationDepth--; }
    }
    internal void Notify(Action action, string stage)
    {
        _notificationDepth++;
        try { action(); }
        catch (Exception ex) { Report(ex, stage + "; remaining notification chain aborted"); throw; }
        finally { _notificationDepth--; }
    }
    internal void PublishChanges(LifecycleContext operation, LifecycleContext? source = null, bool continueOnFailure = false)
    {
        var nodes = new List<UINode>();
        void Visit(UINode node) { nodes.Add(node); foreach (var child in node.Children) Visit(child); }
        Visit(Root);
        var changes = nodes.Select(n => (Node: n, Visible: n.CommittedVisible, Active: n.IsActive,
            OldVisible: n.PublishedVisible, OldActive: n.PublishedActive)).ToArray();
        // Commit all effective state before delivering any notification. Failures are never replayed.
        foreach (var c in changes) { c.Node.PublishedVisible = c.Visible; c.Node.PublishedActive = c.Active; }
        Exception? first = null;
        void Deliver(Action callback)
        {
            try { callback(); }
            catch (Exception ex)
            {
                Report(ex, $"State notification {operation.Node.Name} ({operation.Node.Id})");
                if (!continueOnFailure) throw;
                if (first == null) first = ex; else UIFailure.AddSecondary(first, ex);
            }
        }
        LifecycleContext Describe(UINode node)
        {
            for (var n = node; n != null; n = n.Parent)
                if (source != null && n == source.Node) return source.For(node);
            return operation.For(node);
        }
        foreach (var c in Enumerable.Reverse(changes))
        {
            var context = Describe(c.Node);
            if (c.OldActive && !c.Active) Deliver(() => c.Node.NotifyActive(new StateChangeContext(context, true, false)));
            if (c.OldVisible && !c.Visible) Deliver(() => c.Node.NotifyVisible(new StateChangeContext(context, true, false)));
        }
        foreach (var c in changes)
        {
            var context = Describe(c.Node);
            if (!c.OldVisible && c.Visible) Deliver(() => c.Node.NotifyVisible(new StateChangeContext(context, false, true)));
            if (!c.OldActive && c.Active) Deliver(() => c.Node.NotifyActive(new StateChangeContext(context, false, true)));
        }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }

    internal bool AllowsInput(UINode node)
    {
        if (InputOwner == null) return true;
        for (var n = node; n != null; n = n.Parent) if (ReferenceEquals(n, InputOwner)) return true;
        for (var n = InputOwner; n != null; n = n.Parent) if (ReferenceEquals(n, node)) return true;
        return false;
    }

    internal void SetInputOwner(UINode? node) { InputOwner = node; Root.ApplyState(); }
    private int _modalInputBlocks;
    internal bool IsModalInputBlocked => _modalInputBlocks != 0;
    internal void BeginModalInputBlock() { _modalInputBlocks++; Root.SetInputLocked(true); }
    internal void EndModalInputBlock()
    {
        if (_modalInputBlocks <= 0) throw new InvalidOperationException("Modal input block is unbalanced.");
        _modalInputBlocks--;
        Root.SetInputLocked(_modalInputBlocks != 0);
    }

    public void Register<T>(string? resource = null, WindowInstanceMode windowMode = WindowInstanceMode.Single) where T : UINode, new() =>
        Register(() => new T(), resource, windowMode);

    public void Register<T>(Func<T> factory, string? resource = null, WindowInstanceMode windowMode = WindowInstanceMode.Single) where T : UINode
    {
        try
        {
            Host.VerifyAccess();
            VerifyMutation();
            if (Root.State != NodeState.Ready) throw new ObjectDisposedException(nameof(UIContext));
            if (_registrations.ContainsKey(typeof(T))) throw new InvalidOperationException($"Node {typeof(T).FullName} is already registered.");
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (windowMode != WindowInstanceMode.Single && windowMode != WindowInstanceMode.Multiple)
                throw new ArgumentOutOfRangeException(nameof(windowMode));
            _registrations.Add(typeof(T), new Registration(() => factory(), resource, windowMode));
        }
        catch (Exception ex) { Report(ex, $"Register {typeof(T).FullName}"); throw; }
    }

    internal WindowInstanceMode GetWindowMode<T>() where T : UINode =>
        _registrations.TryGetValue(typeof(T), out var registration) ? registration.WindowMode :
            throw new KeyNotFoundException($"Node {typeof(T).FullName} has not been registered.");

    public async Task<UIResourceLease> PreloadAsync<T>(CancellationToken cancellationToken = default) where T : UINode
    {
        try
        {
            Host.VerifyAccess(); VerifyMutation();
            if (!_registrations.TryGetValue(typeof(T), out var registration))
                throw new KeyNotFoundException($"Node {typeof(T).FullName} has not been registered.");
            if (registration.Resource == null) throw new InvalidOperationException($"Node {typeof(T).FullName} has no resource to preload.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Root.Lifetime);
            return await _resources.AcquireAsync(registration.Resource, linked.Token);
        }
        catch (Exception ex) { Report(ex, $"Preload {typeof(T).FullName}"); throw; }
    }

    internal async Task<T> CreateAsync<T>(UINode parent, object? args, CancellationToken token) where T : UINode
    {
        Host.VerifyAccess();
        token.ThrowIfCancellationRequested();
        if (!_registrations.TryGetValue(typeof(T), out var registration))
            throw new KeyNotFoundException($"Node {typeof(T).FullName} has not been registered.");
        var node = (T)registration.Factory();
        if (node.State != NodeState.Detached) throw new InvalidOperationException("A factory must return a fresh detached node.");
        IUIElement? element = null;
        UIResourceLease? lease = null;
        try
        {
            node.SetParameters(args);
            if (registration.Resource != null)
            {
                lease = await _resources.AcquireAsync(registration.Resource, token);
                token.ThrowIfCancellationRequested();
                element = lease.Resource.Instantiate(parent.Element)
                    ?? throw new InvalidOperationException($"Resource '{registration.Resource}' returned no instance.");
                node.ResourceLease = lease;
            }
            token.ThrowIfCancellationRequested();
            await node.AttachAsync(parent, typeof(T).Name, element, token);
            return node;
        }
        catch (Exception ex)
        {
            Report(ex, $"Create {typeof(T).FullName}");
            await CleanupAsync(ex, async () =>
            {
                if (node.State != NodeState.Detached) await node.DisposeAsync();
                else if (element != null) await element.DisposeAsync();
            });
            await CleanupAsync(ex, () => { lease?.Dispose(); return Task.CompletedTask; });
            throw;
        }
    }

    internal void Report(Exception exception, string context)
    {
        const string key = "UIFramework.Logged";
        if (exception.Data.Contains(key)) return;
        exception.Data[key] = true;
        try { Log.Error(exception, context); }
        catch (Exception logFailure) { UIFailure.AddSecondary(exception, logFailure); Console.Error.WriteLine(logFailure); }
    }

    internal async Task CleanupAsync(Exception primary, Func<Task> cleanup)
    {
        try { await cleanup(); }
        catch (Exception secondary) { Report(secondary, "Cleanup after failure"); UIFailure.AddSecondary(primary, secondary); }
    }

    public ValueTask DisposeAsync()
    {
        Host.VerifyAccess(); VerifyMutation();
        // Root validates reentrant disposal before a context disposal task is retained.
        var root = Root.DisposeAsync();
        return new ValueTask(_disposeTask ??= DisposeCoreAsync(root));
    }
    private async Task DisposeCoreAsync(ValueTask root)
    {
        try { await root; }
        catch (Exception ex)
        {
            await CleanupAsync(ex, _resources.DisposeAsync);
            throw;
        }
        await _resources.DisposeAsync();
    }
}
