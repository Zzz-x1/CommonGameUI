using System.Runtime.CompilerServices;

namespace UIFramework.Mvvm;

public sealed class MvvmRuntime : IAsyncDisposable
{
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<object, object> _shared = new(ReferenceComparer.Instance);
    private readonly List<(object Value, IViewModelAssets? Assets)> _owned = new();
    private readonly List<BindingScope> _scopes = new();
    private readonly List<Task> _tasks = new();
    private readonly HashSet<(IMvvmObject Vm, string Name)> _running = new();
    internal event Action? CommandStateChanged;
    internal bool IsRunning(IMvvmObject vm, string name) => _running.Contains((vm, name));
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _dispose;
    public IUILog Log { get; }
    public bool IsDisposed => _dispose?.IsCompleted == true;
    public void Observe(Task task) => _tasks.Add(task);
    public Task WhenIdleAsync() => Task.WhenAll(_tasks.ToArray());
    public MvvmRuntime(IUILog? log = null) => Log = log ?? new ConsoleUILog();
    public void RegisterFactory<T>(Func<T> factory) where T : class => _factories.Add(typeof(T), () => factory());
    public void RegisterFactory(Type type, Func<object> factory) => _factories.Add(type, factory);
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
    internal void Remove(BindingScope scope) => _scopes.Remove(scope);
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
        if (!_running.Add((vm, command))) throw new InvalidOperationException("Command is already running: " + command);
        var task = Run(); _tasks.Add(task); return task;
        async Task Run()
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(scope.Lifetime, _lifetime.Token);
            Exception? first = null;
            try { CommandStateChanged?.Invoke(); await vm.Execute(command, parameter, linked.Token); }
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
            _tasks.Remove(task); task.GetAwaiter().GetResult();
        }
    }
    public ValueTask DisposeAsync()
    {
        if (_dispose == null)
        {
            var completion = new TaskCompletionSource<Task>();
            _dispose = completion.Task.Unwrap(); completion.SetResult(DisposeCoreAsync());
        }
        return new ValueTask(_dispose);
    }
    private async Task DisposeCoreAsync()
    {
        Exception? first = null;
        try { _lifetime.Cancel(); } catch (Exception error) { Failure(ref first, error); }
        foreach (var scope in _scopes.ToArray())
            try { await scope.DisposeAsync(); } catch (Exception error) { Failure(ref first, error); }
        try { await Task.WhenAll(_tasks.ToArray()); } catch (Exception error) { Failure(ref first, error); }
        foreach (var owned in _owned.AsEnumerable().Reverse())
            try { await Release(owned.Value, owned.Assets); } catch (Exception error) { Failure(ref first, error); }
        _owned.Clear(); _shared.Clear(); _lifetime.Dispose();
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
    private readonly MvvmRuntime _runtime;
    private readonly Func<UINode, IViewBindingConfiguration?>? _configuration;
    private readonly IViewModelAssets? _assets;
    public MvvmNodeExtension(MvvmRuntime runtime, Func<UINode, IViewBindingConfiguration?>? configuration = null, IViewModelAssets? assets = null)
    { _runtime = runtime; _configuration = configuration; _assets = assets; }
    public void BeforeInitialize(UINode node)
    {
        var configuration = _configuration?.Invoke(node);
        var scope = new BindingScope(node, _runtime, _assets); MvvmExtensions.Scopes.Add(node, scope);
        if (configuration != null) scope.Configure(configuration);
    }
    public void AfterInitialize(UINode node) => MvvmExtensions.Scope(node).ApplyConfiguration();
    public ValueTask DisposeAsync(UINode node) => MvvmExtensions.Scope(node).DisposeAsync();
}
public static class MvvmExtensions
{
    internal static readonly ConditionalWeakTable<UINode, BindingScope> Scopes = new();
    internal static BindingScope Scope(UINode node) => Scopes.TryGetValue(node, out var scope) ? scope : throw new InvalidOperationException("Call UseMvvm before creating this node.");
    public static void UseMvvm(this UIContext ui, MvvmRuntime runtime) => ui.AddNodeExtension(new MvvmNodeExtension(runtime));
    public static void ObserveMvvmTask(this UINode node, Task task) => Scope(node).Observe(task);
    public static void SetViewModel(this UINode node, string name, object vm) => Scope(node).Set(name, vm);
    public static T GetViewModel<T>(this UINode node, string name) where T : class => (T)Scope(node).Get(name);
    public static T CreateViewModel<T>(this UINode node, string name, object? template = null) where T : class => Scope(node).Create<T>(name, template);
    public static IDisposable Bind(this UINode node, string path, IBindingTarget target, BindingMode mode = BindingMode.OneWay) =>
        Scope(node).Bind(path, target, new BindingOptions { Mode = mode });
    public static IDisposable Bind(this UINode node, string path, IBindingTarget target, BindingOptions options) => Scope(node).Bind(path, target, options);
    public static IDisposable BindCommand(this UINode node, string path, ICommandTarget target, object? parameter = null) => Scope(node).Command(path, target, parameter);
    public static IDisposable BindCollection(this UINode node, string path, ICollectionTarget target, bool oneTime = false) => Scope(node).Collection(path, target, oneTime);
}
