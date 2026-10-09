using System.Runtime.CompilerServices;

namespace UIFramework.Mvvm;

public sealed class MvvmRuntime : IAsyncDisposable
{
    static MvvmRuntime() => MvvmModule.Install();
    private readonly Dictionary<object, IGlobalViewModelProvider> _globals = new(ReferenceComparer.Instance);
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<object, object> _shared = new(ReferenceComparer.Instance);
    private readonly List<(object Value, IViewModelAssets? Assets)> _owned = new();
    private readonly List<BindingScope> _scopes = new();
    private readonly List<Task> _tasks = new();
    private readonly HashSet<(IMvvmObject Vm, string Name)> _running = new();
    internal event Action? CommandStateChanged;
    internal bool IsRunning(IMvvmObject vm, string name) => _running.Contains((vm, name));
    private readonly List<CommandExecution> _commands = new();
    private readonly HashSet<Task> _releases = new();
    private Task? _dispose;
    private Task? _close;
    private bool _observeDisposal;
    public IUILog Log { get; }
    public bool IsDisposed => _dispose?.IsCompleted == true;
    public void Observe(Task task) => _tasks.Add(task);
    public async Task WhenIdleAsync()
    {
        await DrainTasksAsync();
        if (_dispose != null) await _dispose;
    }
    private async Task DrainTasksAsync()
    {
        var waited = new HashSet<Task>();
        Exception? first = null;
        while (true)
        {
            var tasks = _tasks.Where(task => waited.Add(task)).ToArray();
            if (tasks.Length == 0) break;
            try { await Task.WhenAll(tasks); } catch (Exception error) { Failure(ref first, error); }
        }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    internal void ObserveRelease(Task task) { _releases.Add(task); Observe(task); }
    public MvvmRuntime(IUILog? log = null) => Log = log ?? new ConsoleUILog();
    public void RegisterFactory<T>(Func<T> factory) where T : class => _factories.Add(typeof(T), () => factory());
    public void RegisterFactory(Type type, Func<object> factory) => _factories.Add(type, factory);
    public void RegisterGlobalViewModelProvider(IGlobalViewModelProvider provider)
    {
        if (_dispose != null) throw new ObjectDisposedException(nameof(MvvmRuntime));
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        var definitions = provider.ViewModels.ToArray();
        if (definitions.Any(value => value == null || !provider.Assets.IsAsset(value)))
            throw new ArgumentException("Global ViewModels must be native woven VM assets.");
        if (definitions.Distinct(ReferenceComparer.Instance).Count() != definitions.Length || definitions.Any(_globals.ContainsKey))
            throw new ArgumentException("A global VM asset is registered more than once.");
        foreach (var definition in definitions) _globals.Add(definition, provider);
    }
    public T GetGlobalViewModel<T>(object definition) where T : class
    {
        if (_dispose != null) throw new ObjectDisposedException(nameof(MvvmRuntime));
        var provider = _globals.TryGetValue(definition, out var registered) ? registered
            : throw new KeyNotFoundException("VM asset is not registered by a global provider.");
        return (T)Create(typeof(T), definition, provider.Assets, true, (_, _) => { });
    }
    public T GetSharedViewModel<T>(T template, IViewModelAssets assets) where T : class => (T)Create(typeof(T), template, assets, true, (_, _) => { });
    internal object Create(Type type, object? template, IViewModelAssets? assets, bool shared, Action<object, IViewModelAssets?> own)
    {
        if (_dispose != null) throw new ObjectDisposedException(nameof(MvvmRuntime));
        var graph = shared ? _shared : new Dictionary<object, object>(ReferenceComparer.Instance);
        var added = new List<object>();
        try { return Resolve(type, template); }
        catch { foreach (var key in added) graph.Remove(key); throw; }
        object Resolve(Type requestedType, object? source)
        {
            if (source != null && graph.TryGetValue(source, out var cached)) return cached;
            if (shared && source == null) throw new ArgumentException("Shared Asset requires a template.");
            object value;
            if (source != null)
            {
                if (assets == null || !assets.IsAsset(source)) throw new ArgumentException("A native VM asset is required.");
                value = assets.Clone(source);
            }
            else value = _factories.TryGetValue(requestedType, out var factory) ? factory() : assets?.CanCreate(requestedType) == true ? assets.CreateInstance(requestedType) : Activator.CreateInstance(requestedType)
                ?? throw new InvalidOperationException("VM factory returned null.");
            var adapter = assets?.IsAsset(value) == true ? assets : null;
            if (shared) _owned.Add((value, adapter)); else own(value, adapter);
            if (!requestedType.IsInstanceOfType(value)) throw new InvalidCastException("VM factory/template type mismatch: " + requestedType.FullName);
            if (value is not IMvvmObject) throw new InvalidOperationException("ViewModel has not been woven: " + requestedType.FullName);
            if (source != null)
            {
                graph.Add(source, value); added.Add(source);
                assets!.Remap(source, value, nested => Resolve(nested.GetType(), nested));
            }
            return value;
        }
    }
    internal void Add(BindingScope scope) => _scopes.Add(scope);
    internal void Remove(BindingScope scope)
    {
        _scopes.Remove(scope);
        _commands.RemoveAll(command => ReferenceEquals(command.Scope, scope) && command.Completion.IsCompleted);
    }
    internal async ValueTask Release(object value, IViewModelAssets? assets)
    {
        Exception? first = null;
        try
        {
            if (value is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
            else if (value is IDisposable disposable) disposable.Dispose();
        }
        catch (Exception error) { Failure(ref first, error); }
        try { assets?.Release(value); } catch (Exception error) { Failure(ref first, error); }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    internal Task Execute(BindingScope scope, IMvvmObject vm, string command, object? parameter)
    {
        if (_dispose != null) throw new ObjectDisposedException(nameof(MvvmRuntime));
        if (!_running.Add((vm, command))) throw new InvalidOperationException("Command is already running: " + command);
        var execution = new CommandExecution(scope);
        _commands.Add(execution); scope.Add(execution); _tasks.Add(execution.Completion);
        execution.Start(Run); return execution.Completion;
        async Task Run()
        {
            Exception? first = null;
            try { CommandStateChanged?.Invoke(); await vm.Execute(command, parameter, execution.Token); }
            catch (Exception error) { first = error; Report(error, "MVVM command " + command); }
            _running.Remove((vm, command));
            try { CommandStateChanged?.Invoke(); } catch (Exception error) { Failure(ref first, error); }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
    }
    /// <summary>Engine adapters call this from their persistent frame observer.</summary>
    public void ObserveTasks()
    {
        foreach (var task in _tasks.ToArray())
        {
            if (!task.IsCompleted) continue;
            _tasks.Remove(task); _releases.Remove(task); task.GetAwaiter().GetResult();
        }
        if (_observeDisposal && _dispose?.IsCompleted == true)
        {
            _observeDisposal = false;
            _dispose.GetAwaiter().GetResult();
        }
    }
    public ValueTask DisposeAsync()
    {
        var caller = _commands.Any(command => command.IsCaller);
        if (_dispose == null)
        {
            _observeDisposal = caller;
            var closed = new TaskCompletionSource<Task>();
            var disposed = new TaskCompletionSource<Task>();
            _close = closed.Task.Unwrap(); _dispose = disposed.Task.Unwrap();
            closed.SetResult(CloseAsync()); disposed.SetResult(DisposeCoreAsync());
        }
        return new ValueTask(caller ? _close! : _dispose);
    }
    private async Task CloseAsync()
    {
        Exception? first = null;
        foreach (var command in _commands.Where(command => !command.IsCaller).ToArray())
            try { command.Cancel(); } catch (Exception error) { Failure(ref first, error); }
        foreach (var scope in _scopes.ToArray())
            try { await scope.DisposeAsync(); } catch (Exception error) { Failure(ref first, error); }
        var callers = new HashSet<Task>(_commands.Where(command => command.IsCaller).Select(command => command.Completion));
        try { await Task.WhenAll(_tasks.Where(task => !callers.Contains(task) && !_releases.Contains(task)).ToArray()); }
        catch (Exception error) { Failure(ref first, error); }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    private async Task DisposeCoreAsync()
    {
        Exception? first = null;
        try { await _close!; } catch (Exception error) { Failure(ref first, error); }
        try { await DrainTasksAsync(); } catch (Exception error) { Failure(ref first, error); }
        foreach (var owned in _owned.AsEnumerable().Reverse())
            try { await Release(owned.Value, owned.Assets); } catch (Exception error) { Report(error, "MVVM shared VM release"); Failure(ref first, error); }
        _owned.Clear(); _shared.Clear(); _globals.Clear(); _commands.Clear(); _releases.Clear(); _tasks.Clear();
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    internal static void Failure(ref Exception? first, Exception error)
    {
        if (ReferenceEquals(first, error)) return;
        if (first == null) first = error;
        else
        {
            if (first.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> secondary)
                first.Data[UIFailure.SecondaryExceptionsKey] = secondary = new List<Exception>();
            secondary.Add(error);
        }
    }
    internal void Report(Exception error, string context)
    { try { Log.Error(error, context); } catch (Exception loggingError) { Exception? first = error; Failure(ref first, loggingError); } }
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

public sealed class MvvmNodeExtension : IUINodeExtension
{
    private MvvmRuntime _runtime;
    private Func<UINode, IViewBindingConfiguration?>? _configuration;
    private IViewModelAssets? _assets;
    public MvvmNodeExtension(MvvmRuntime runtime, Func<UINode, IViewBindingConfiguration?>? configuration = null, IViewModelAssets? assets = null)
    { _runtime = runtime; _configuration = configuration; _assets = assets; }
    internal void Configure(MvvmRuntime runtime, Func<UINode, IViewBindingConfiguration?>? configuration, IViewModelAssets? assets)
    { _runtime = runtime; _configuration = configuration; _assets = assets; }
    public void BeforeInitialize(UINode node)
    {
        var configuration = _configuration?.Invoke(node);
        var scope = new BindingScope(node, _runtime, _assets); MvvmExtensions.Scopes.Add(node, scope);
        if (configuration != null) scope.Configure(configuration);
        if (MvvmExtensions.Initializers.TryGetValue(node, out var initialize))
        { MvvmExtensions.Initializers.Remove(node); initialize(node.Owner); }
    }
    public Task AfterInitializeAsync(UINode node, CancellationToken token) => MvvmExtensions.Scope(node).InitializeAsync(token);
    public ValueTask DisposeAsync(UINode node) => MvvmExtensions.Scope(node).DisposeAsync();
}
public static class MvvmExtensions
{
    internal static readonly ConditionalWeakTable<UINode, BindingScope> Scopes = new();
    internal static readonly ConditionalWeakTable<UINode, Action<IUINode>> Initializers = new();
    internal static BindingScope Scope(UINode node) => Scopes.TryGetValue(node, out var scope) ? scope : throw new InvalidOperationException("MVVM was not installed when this node was created.");
    public static MvvmRuntime GetMvvm(this UIContext ui) => MvvmModule.Contexts.GetValue(ui, _ => throw new InvalidOperationException("MVVM module must be installed before creating the context.")).Runtime;
    public static void ConfigureMvvm(this UIContext ui, MvvmRuntime? runtime = null,
        Func<UINode, IViewBindingConfiguration?>? configuration = null, IViewModelAssets? assets = null) => MvvmModule.Contexts.GetValue(ui, _ => throw new InvalidOperationException("MVVM module is not installed.")).Configure(runtime, configuration, assets);
    public static T GetGlobalViewModel<T>(this UIContext ui, object definition) where T : class => ui.GetMvvm().GetGlobalViewModel<T>(definition);
    public static void ShareMvvm(this UIContext ui, UIContext owner) => ui.ConfigureMvvm(owner.GetMvvm());
    public static void RegisterViewModelFactory<T>(this UIContext ui, Func<T> factory) where T : class => ui.GetMvvm().RegisterFactory(factory);
    public static Task WhenMvvmIdleAsync(this UIContext ui) => ui.GetMvvm().WhenIdleAsync();
    public static void ObserveMvvmTask(this IUINode node, Task task) => Scope(node.Node).Observe(task);
    public static void SetViewModel(this IUINode node, string name, object vm) => Scope(node.Node).Set(name, vm);
    public static T GetViewModel<T>(this IUINode node, string name) where T : class => (T)Scope(node.Node).Get(name);
    public static T CreateViewModel<T>(this IUINode node, string name, object? template = null) where T : class => Scope(node.Node).Create<T>(name, template);
    public static IDisposable Bind(this IUINode node, string path, IBindingTarget target, BindingMode mode = BindingMode.OneWay) =>
        Scope(node.Node).Bind(path, target, new BindingOptions { Mode = mode });
    public static IDisposable Bind(this IUINode node, string path, IBindingTarget target, BindingOptions options) => Scope(node.Node).Bind(path, target, options);
    public static void PauseBinding(this IUINode node, string propertyName) => Scope(node.Node).PauseBinding(propertyName);
    public static void ResumeBinding(this IUINode node, string propertyName) => Scope(node.Node).ResumeBinding(propertyName);
    public static IDisposable BindCommand(this IUINode node, string path, ICommandTarget target, object? parameter = null) => Scope(node.Node).Command(path, target, parameter);
    public static IDisposable BindCommand(this IUINode node, string path, ICommandTarget target, CommandBindingOptions options) => Scope(node.Node).Command(path, target, options);
    public static IDisposable BindCollection(this IUINode node, string path, ICollectionTarget target, bool oneTime = false) => Scope(node.Node).Collection(path, target, oneTime);
}
