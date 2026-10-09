using System.Runtime.CompilerServices;

namespace UIFramework.Mvvm;

public static class MvvmModule
{
    private static bool _installed;
    private static readonly Dictionary<Type, Adapter> Adapters = new();
    internal static readonly ConditionalWeakTable<UIContext, ContextService> Contexts = new();
    private static readonly ConditionalWeakTable<MvvmRuntime, RuntimeLease> Leases = new();
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        UIContextExtensions.Register(() => new ContextService());
    }
    public static void RegisterAdapter(Type hostType, IViewModelAssets assets,
        Func<UINode, IViewBindingConfiguration?> configuration, Action<MvvmRuntime> observe,
        Func<IEnumerable<IGlobalViewModelProvider>>? globalProviders = null)
    { Install(); Adapters[hostType] = new Adapter(assets, configuration, observe, globalProviders); }
    private sealed class Adapter
    {
        internal readonly Func<IEnumerable<IGlobalViewModelProvider>>? GlobalProviders;
        internal readonly IViewModelAssets Assets;
        internal readonly Func<UINode, IViewBindingConfiguration?> Configuration;
        internal readonly Action<MvvmRuntime> Observe;
        internal Adapter(IViewModelAssets assets, Func<UINode, IViewBindingConfiguration?> configuration, Action<MvvmRuntime> observe, Func<IEnumerable<IGlobalViewModelProvider>>? globalProviders)
        { Assets = assets; Configuration = configuration; Observe = observe; GlobalProviders = globalProviders; }
    }
    private sealed class RuntimeLease
    {
        internal readonly MvvmRuntime Runtime;
        internal int References;
        internal RuntimeLease(MvvmRuntime runtime) => Runtime = runtime;
        internal ValueTask Release() => --References == 0 ? Runtime.DisposeAsync() : default;
    }
    internal sealed class ContextService : IUIContextExtension
    {
        private UIContext _ui = null!;
        private RuntimeLease _lease = null!;
        private MvvmNodeExtension _nodes = null!;
        private Adapter? _adapter;
        private readonly List<Task> _retired = new();
        internal MvvmRuntime Runtime => _lease.Runtime;
        public void Initialize(UIContext ui)
        {
            _ui = ui; Contexts.Add(ui, this); Adapters.TryGetValue(ui.Host.GetType(), out _adapter);
            SetRuntime(new MvvmRuntime(ui.Log));
            foreach (var provider in _adapter?.GlobalProviders?.Invoke() ?? Enumerable.Empty<IGlobalViewModelProvider>())
                Runtime.RegisterGlobalViewModelProvider(provider);
            foreach (var attribute in AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetCustomAttributes(typeof(GlobalViewModelProviderAttribute), false).Cast<GlobalViewModelProviderAttribute>()))
                Runtime.RegisterGlobalViewModelProvider((IGlobalViewModelProvider)Activator.CreateInstance(attribute.ProviderType)!);
            _nodes = new MvvmNodeExtension(Runtime, _adapter?.Configuration, _adapter?.Assets);
            ui.AddNodeExtension(_nodes);
        }
        private void SetRuntime(MvvmRuntime runtime)
        { _lease = Leases.GetValue(runtime, value => new RuntimeLease(value)); _lease.References++; _adapter?.Observe(runtime); }
        internal void Configure(MvvmRuntime? runtime, Func<UINode, IViewBindingConfiguration?>? configuration, IViewModelAssets? assets)
        {
            _ui.Host.VerifyAccess();
            if (_ui.Root.State != NodeState.Ready || _ui.Root.Children.Count != 0) throw new InvalidOperationException("Configure MVVM before creating nodes.");
            if (runtime != null && !ReferenceEquals(runtime, Runtime)) { _retired.Add(_lease.Release().AsTask()); SetRuntime(runtime); }
            _nodes.Configure(Runtime, configuration ?? _adapter?.Configuration, assets ?? _adapter?.Assets);
        }
        public async ValueTask DisposeAsync()
        {
            Exception? first = null;
            try { await Task.WhenAll(_retired); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            try { await _lease.Release(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
    }
}
