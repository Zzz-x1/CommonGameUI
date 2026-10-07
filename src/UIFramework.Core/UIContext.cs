namespace UIFramework;

public sealed class UIContext : IAsyncDisposable
{
    private readonly SynchronizationContext? _synchronization = SynchronizationContext.Current;
    internal void Post(Action action)
    {
        if (_synchronization != null) _synchronization.Post(_ => action(), null);
        else ThreadPool.QueueUserWorkItem(_ => action());
    }
    private sealed class Registration(Func<IUINode>? factory, string? resource, WindowInstanceMode windowMode, Action<IUINode>? configure, NodeCacheMode cacheMode)
    {
        public Func<IUINode>? Factory { get; } = factory;
        public string? Resource { get; } = resource;
        public WindowInstanceMode WindowMode { get; } = windowMode;
        public NodeCacheMode CacheMode { get; } = cacheMode;
        public Action<IUINode>? Configure { get; } = configure;
    }
    private readonly Dictionary<Type, Registration> _registrations = new();
    private readonly UIResources _resources;
    private readonly List<IUINodeExtension> _extensions = new();
    internal IReadOnlyList<IUINodeExtension> NodeExtensions => _extensions;
    public void AddNodeExtension(IUINodeExtension extension)
    {
        Host.VerifyAccess(); VerifyMutation();
        if (extension == null) throw new ArgumentNullException(nameof(extension));
        if (Root.Children.Count != 0 || Root.State != NodeState.Ready)
            throw new InvalidOperationException("Configure node extensions before creating nodes.");
        if (_extensions.Contains(extension)) throw new InvalidOperationException("Extension already registered.");
        _extensions.Add(extension);
    }
    private Task? _disposeTask;
    private readonly IUIContextExtension[] _contextExtensions;

    public UIContext(IUIHost host, IUIResourceLoader loader, IUILog? log = null)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Loader = loader ?? throw new ArgumentNullException(nameof(loader));
        Log = log ?? new ConsoleUILog();
        Host.VerifyAccess();
        Root = new UINode();
        Root.InitializeRoot(this);
        _resources = new UIResources(this);
        _contextExtensions = UIContextExtensions.Create();
        foreach (var extension in _contextExtensions) extension.Initialize(this);
    }

    public UINode Root { get; }
    public IUIHost Host { get; }
    public IUIResourceLoader Loader { get; }
    public IUILog Log { get; }
    public IUINode? InputOwner => InputOwnerNode?.Owner;
    internal UINode? InputOwnerNode { get; private set; }
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

    private IUIBackdropFactory? _defaultBackdropFactory;
    public IUIBackdropFactory? DefaultBackdropFactory
    {
        get => _defaultBackdropFactory;
        set
        {
            try
            {
                Host.VerifyAccess(); VerifyMutation();
                if (Root.State != NodeState.Ready) throw new ObjectDisposedException(nameof(UIContext));
                _defaultBackdropFactory = value;
            }
            catch (Exception ex) { Report(ex, "Configure default backdrop"); throw; }
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
        void Visit(UINode node) { nodes.Add(node); foreach (var child in node.ChildrenCore) Visit(child); }
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
                if (source != null && ReferenceEquals(n.Owner, source.Node)) return source.For(node);
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
        if (InputOwnerNode == null) return true;
        for (var n = node; n != null; n = n.Parent) if (ReferenceEquals(n, InputOwnerNode)) return true;
        for (var n = InputOwnerNode; n != null; n = n.Parent) if (ReferenceEquals(n, node)) return true;
        return false;
    }

    internal void SetInputOwner(UINode? node) { InputOwnerNode = node; Root.ApplyState(); }
    private int _modalInputBlocks;
    internal bool IsModalInputBlocked => _modalInputBlocks != 0;
    internal void BeginModalInputBlock() { _modalInputBlocks++; Root.SetInputLocked(true); }
    internal void EndModalInputBlock()
    {
        if (_modalInputBlocks <= 0) throw new InvalidOperationException("Modal input block is unbalanced.");
        _modalInputBlocks--;
        Root.SetInputLocked(_modalInputBlocks != 0);
    }

    public void Register<T>(string resource, Action<T>? configure = null, WindowInstanceMode windowMode = WindowInstanceMode.Single, NodeCacheMode cacheMode = NodeCacheMode.None) where T : class, IUINode =>
        RegisterCore<T>(null, resource, windowMode, configure, cacheMode);

    public void Register<T>(Func<T> factory, string? resource = null, WindowInstanceMode windowMode = WindowInstanceMode.Single, Action<T>? configure = null, NodeCacheMode cacheMode = NodeCacheMode.None) where T : class, IUINode
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        RegisterCore(factory, resource, windowMode, configure, cacheMode);
    }

    private void RegisterCore<T>(Func<T>? factory, string? resource, WindowInstanceMode windowMode, Action<T>? configure, NodeCacheMode cacheMode) where T : class, IUINode
    {
        try
        {
            Host.VerifyAccess(); VerifyMutation();
            if (Root.State != NodeState.Ready) throw new ObjectDisposedException(nameof(UIContext));
            if (_registrations.ContainsKey(typeof(T))) throw new InvalidOperationException($"Node {typeof(T).FullName} is already registered.");
            if (factory == null && string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("A native node requires a resource.", nameof(resource));
            if (windowMode != WindowInstanceMode.Single && windowMode != WindowInstanceMode.Multiple) throw new ArgumentOutOfRangeException(nameof(windowMode));
            if (!Enum.IsDefined(typeof(NodeCacheMode), cacheMode)) throw new ArgumentOutOfRangeException(nameof(cacheMode));
            _registrations.Add(typeof(T), new Registration(factory == null ? null : () => factory(), resource, windowMode,
                configure == null ? null : owner => configure((T)owner), cacheMode));
        }
        catch (Exception ex) { Report(ex, $"Register {typeof(T).FullName}"); throw; }
    }

    internal WindowInstanceMode GetWindowMode<T>() where T : class, IUINode =>
        _registrations.TryGetValue(typeof(T), out var registration) ? registration.WindowMode :
            throw new KeyNotFoundException($"Node {typeof(T).FullName} has not been registered.");

    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        Host.VerifyAccess(); VerifyMutation();
        if (Root.State != NodeState.Ready) throw new ObjectDisposedException(nameof(UIContext));
        var containers = new List<ContainerNode>();
        void Collect(UINode node)
        {
            foreach (var child in node.ChildrenCore.ToArray()) Collect(child);
            if (node is ContainerNode container) containers.Add(container);
        }
        Collect(Root);
        Exception? first = null;
        foreach (var container in containers)
        {
            if (first != null && cancellationToken.IsCancellationRequested) break;
            cancellationToken.ThrowIfCancellationRequested();
            try { await container.ClearContextCacheAsync(cancellationToken); }
            catch (Exception ex) { Report(ex, "Clear context cache"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex); }
        }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }

    public async Task<UIResourceLease> PreloadAsync<T>(CancellationToken cancellationToken = default) where T : class, IUINode
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

    internal async Task<UINode> CreateAsync<T>(UINode parent, object? args, CancellationToken token) where T : class, IUINode
    {
        Host.VerifyAccess();
        token.ThrowIfCancellationRequested();
        if (!_registrations.TryGetValue(typeof(T), out var registration))
            throw new KeyNotFoundException($"Node {typeof(T).FullName} has not been registered.");
        UINode? node = null;
        IUIElement? element = null;
        UIResourceLease? lease = null;
        try
        {
            IUINode? owner = registration.Factory == null ? null : registration.Factory()
                ?? throw new InvalidOperationException("A node factory returned null.");
            if (owner != null)
            {
                node = owner.Node;
                if (!ReferenceEquals(node.Owner, owner) || node.State != NodeState.Detached)
                    throw new InvalidOperationException("A factory must provide a fresh, detached node.");
                node.SetParameters(args);
            }
            if (registration.Resource != null)
            {
                lease = await _resources.AcquireAsync(registration.Resource, token);
                token.ThrowIfCancellationRequested();
                element = lease.Resource.Instantiate(parent.Element)
                    ?? throw new InvalidOperationException($"Resource '{registration.Resource}' returned no instance.");
            }
            owner ??= element is IUIResourceNodeProvider provider ? provider.GetNode(typeof(T)) :
                throw new InvalidOperationException($"Resource '{registration.Resource}' does not provide native business nodes.");
            if (owner is not T) throw new InvalidOperationException($"Resource must provide {typeof(T).FullName}.");
            node = owner.Node;
            if (!ReferenceEquals(node.Owner, owner) || node.State != NodeState.Detached)
                throw new InvalidOperationException("Creation must provide a fresh, detached node with the same business identity.");
            if (registration.Factory == null) node.SetParameters(args);
            node.RegisteredType = typeof(T); node.CacheMode = registration.CacheMode;
            node.ResourceLease = lease;
            token.ThrowIfCancellationRequested();
            await node.AttachAsync(parent, typeof(T).Name, element, token, () => registration.Configure?.Invoke(owner));
            return node;
        }
        catch (Exception ex)
        {
            Report(ex, $"Create {typeof(T).FullName}");
            await CleanupAsync(ex, async () =>
            {
                if (node != null && node.State != NodeState.Detached) await node.DisposeAsync();
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
        if (_disposeTask == null)
        {
            var completion = new TaskCompletionSource<Task>();
            _disposeTask = completion.Task.Unwrap();
            completion.SetResult(DisposeCoreAsync(root));
        }
        return new ValueTask(_disposeTask);
    }
    private async Task DisposeCoreAsync(ValueTask root)
    {
        Exception? first = null;
        try { await root; } catch (Exception error) { first = error; }
        try { await _resources.DisposeAsync(); }
        catch (Exception error) { Report(error, "Dispose resources"); if (first == null) first = error; else UIFailure.AddSecondary(first, error); }
        foreach (var extension in Enumerable.Reverse(_contextExtensions))
            try { await extension.DisposeAsync(); }
            catch (Exception error) { Report(error, "Dispose context extension"); if (first == null) first = error; else UIFailure.AddSecondary(first, error); }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
}
