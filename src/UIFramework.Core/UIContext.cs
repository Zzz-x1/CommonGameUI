namespace UIFramework;

public sealed class UIContext : IAsyncDisposable
{
    private sealed class Registration
    {
        public Registration(Func<UINode> factory, string? resource) { Factory = factory; Resource = resource; }
        public Func<UINode> Factory { get; }
        public string? Resource { get; }
    }
    private readonly Dictionary<Type, Registration> _registrations = new();

    public UIContext(IUIHost host, IUIResourceLoader loader, IUILog? log = null)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Loader = loader ?? throw new ArgumentNullException(nameof(loader));
        Log = log ?? new ConsoleUILog();
        Host.VerifyAccess();
        Root = new UINode();
        Root.InitializeRoot(this);
    }

    public UINode Root { get; }
    public IUIHost Host { get; }
    public IUIResourceLoader Loader { get; }
    public IUILog Log { get; }
    public UINode? InputOwner { get; private set; }

    internal bool AllowsInput(UINode node)
    {
        if (InputOwner == null) return true;
        for (var n = node; n != null; n = n.Parent) if (ReferenceEquals(n, InputOwner)) return true;
        for (var n = InputOwner; n != null; n = n.Parent) if (ReferenceEquals(n, node)) return true;
        return false;
    }

    internal void SetInputOwner(UINode? node) { InputOwner = node; Root.ApplyState(); }

    public void Register<T>(string? resource = null) where T : UINode, new() => Register(() => new T(), resource);

    public void Register<T>(Func<T> factory, string? resource = null) where T : UINode
    {
        try
        {
            Host.VerifyAccess();
            if (Root.State != NodeState.Ready) throw new ObjectDisposedException(nameof(UIContext));
            if (_registrations.ContainsKey(typeof(T))) throw new InvalidOperationException($"Node {typeof(T).FullName} is already registered.");
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _registrations.Add(typeof(T), new Registration(() => factory(), resource));
        }
        catch (Exception ex) { Report(ex, $"Register {typeof(T).FullName}"); throw; }
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
        try
        {
            node.SetParameters(args);
            if (registration.Resource != null)
                element = await Loader.LoadAsync(registration.Resource, parent.Element, token);
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

    public ValueTask DisposeAsync() => Root.DisposeAsync();
}
