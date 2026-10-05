using System.Runtime.CompilerServices;

namespace UIFramework.Mvvm;

public sealed class MvvmRuntime : IAsyncDisposable
{
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<object, object> _shared = new(ReferenceComparer.Instance);
    private readonly List<(object Value, IViewModelAssets? Assets)> _owned = new();
    private readonly List<BindingScope> _scopes = new();
    private readonly List<Task> _tasks = new();
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _dispose;
    public IUILog Log { get; }
    public MvvmRuntime(IUILog? log = null) => Log = log ?? new ConsoleUILog();
    public void RegisterFactory<T>(Func<T> factory) where T : class => _factories.Add(typeof(T), () => factory());
    internal object Create(Type type, object? template, IViewModelAssets? assets, bool shared)
    {
        if (_dispose != null) throw new ObjectDisposedException(nameof(MvvmRuntime));
        if (template != null && shared && _shared.TryGetValue(template, out var cached)) return cached;
        object value;
        if (template != null)
        {
            if (assets == null || !assets.IsAsset(template)) throw new ArgumentException("A native VM asset is required.");
            value = assets.Clone(template, nested => Create(nested.GetType(), nested, assets, shared));
        }
        else value = _factories.TryGetValue(type, out var factory) ? factory() : Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("VM factory returned null.");
        if (value is not IMvvmObject) throw new InvalidOperationException("ViewModel has not been woven: " + type.FullName);
        if (shared)
        {
            if (template == null) throw new ArgumentException("Shared Asset requires a template.");
            _shared.Add(template, value); _owned.Add((value, assets));
        }
        return value;
    }
    internal void Add(BindingScope scope) => _scopes.Add(scope);
    internal void Remove(BindingScope scope) => _scopes.Remove(scope);
    internal async ValueTask Release(object value, IViewModelAssets? assets)
    {
        if (value is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else if (value is IDisposable disposable) disposable.Dispose();
        if (assets != null) assets.Release(value);
    }
    internal Task Execute(BindingScope scope, IMvvmObject vm, string command, object? parameter)
    {
        var task = Run(); _tasks.Add(task); return task;
        async Task Run()
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(scope.Node.Lifetime, _lifetime.Token);
            try { await vm.Execute(command, parameter, linked.Token); }
            catch (Exception error) { Log.Error(error, "MVVM command " + command); throw; }
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
        _dispose ??= DisposeCoreAsync(); return new ValueTask(_dispose);
    }
    private async Task DisposeCoreAsync()
    {
        _lifetime.Cancel();
        Exception? first = null;
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
        if (first == null) first = error;
        else
        {
            if (first.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> secondary)
                first.Data[UIFailure.SecondaryExceptionsKey] = secondary = new List<Exception>();
            secondary.Add(error);
        }
    }
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
    public static void SetViewModel(this UINode node, string name, object vm) => Scope(node).Set(name, vm);
    public static T GetViewModel<T>(this UINode node, string name) where T : class => (T)Scope(node).Get(name);
    public static IDisposable Bind(this UINode node, string path, IBindingTarget target, BindingMode mode = BindingMode.OneWay) =>
        Scope(node).Bind(path, target, new BindingOptions { Mode = mode });
    public static IDisposable Bind(this UINode node, string path, IBindingTarget target, BindingOptions options) => Scope(node).Bind(path, target, options);
    public static IDisposable BindCommand(this UINode node, string path, ICommandTarget target, object? parameter = null) => Scope(node).Command(path, target, parameter);
    public static IDisposable BindCollection(this UINode node, string path, ICollectionTarget target, bool oneTime = false) => Scope(node).Collection(path, target, oneTime);
}
