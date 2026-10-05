using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace UIFramework.Mvvm;

internal sealed class BindingScope : IAsyncDisposable
{
    private readonly MvvmRuntime _runtime;
    private readonly IViewModelAssets? _assets;
    private readonly Dictionary<string, object> _sources = new(StringComparer.Ordinal);
    private readonly List<(object Value, IViewModelAssets? Assets)> _owned = new();
    private readonly List<IDisposable> _bindings = new();
    private readonly List<Task> _commands = new();
    private IViewBindingConfiguration? _configuration;
    private Task? _dispose;
    private readonly CancellationTokenSource _lifetime;
    internal CancellationToken Lifetime => _lifetime.Token;
    internal event Action? SourcesChanged;
    internal UINode Node { get; }
    public BindingScope(UINode node, MvvmRuntime runtime, IViewModelAssets? assets = null)
    { Node = node; _runtime = runtime; _assets = assets; _lifetime = CancellationTokenSource.CreateLinkedTokenSource(node.Lifetime); runtime.Add(this); }
    internal void Configure(IViewBindingConfiguration configuration)
    {
        _configuration = configuration;
        foreach (var source in configuration.Sources)
        {
            if (source.Creation == ViewModelCreation.Manual) continue;
            var value = _runtime.Create(source.Type, source.Template, _assets, source.Creation == ViewModelCreation.SharedAsset, (item, adapter) => _owned.Add((item, adapter)));
            _sources.Add(source.Name, value);
        }
    }
    internal void ApplyConfiguration()
    {
        if (_configuration == null) return;
        foreach (var source in _configuration.Sources)
            if (!source.Type.IsInstanceOfType(Get(source.Name))) throw new InvalidCastException("VM source type mismatch: " + source.Name);
        _configuration.Apply(Node);
    }
    public void Set(string name, object value)
    {
        Node.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
        if (value is not IMvvmObject) throw new InvalidOperationException("VM must be woven: " + value.GetType().FullName);
        _sources[name] = value; SourcesChanged?.Invoke();
    }
    public object Get(string name)
    {
        Node.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
        return _sources.TryGetValue(name, out var value) ? value : throw new KeyNotFoundException("Missing VM source: " + name);
    }
    public T Create<T>(string name, object? template) where T : class
    {
        Node.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
        if (_sources.ContainsKey(name)) throw new ArgumentException("Duplicate VM source: " + name);
        var value = (T)_runtime.Create(typeof(T), template, _assets, false, (item, adapter) => _owned.Add((item, adapter)));
        _sources.Add(name, value); SourcesChanged?.Invoke(); return value;
    }
    public IDisposable Bind(string path, IBindingTarget target, BindingOptions options) => Own(new PropertyBinding(this, path, target, options));
    public IDisposable Command(string path, ICommandTarget target, object? parameter) => Own(new CommandBinding(this, path, target, parameter));
    public IDisposable Collection(string path, ICollectionTarget target, bool oneTime) => Own(new CollectionBinding(this, path, target, oneTime));
    private T Own<T>(T binding) where T : IDisposable { _bindings.Add(binding); return binding; }
    internal void Observe(Task task) { _commands.Add(task); _runtime.Observe(task); }
    internal Task Run(IMvvmObject vm, string command, object? parameter)
    { var task = _runtime.Execute(this, vm, command, parameter); _commands.Add(task); return task; }
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
        foreach (var binding in _bindings.AsEnumerable().Reverse())
            try { binding.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        try { _lifetime.Cancel(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        try { await Task.WhenAll(_commands.ToArray()); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        foreach (var value in _owned.AsEnumerable().Reverse())
            try { await _runtime.Release(value.Value, value.Assets); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        _runtime.Remove(this); _sources.Clear(); _bindings.Clear(); _owned.Clear();
        _lifetime.Dispose();
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    internal sealed class Path : IDisposable
    {
        private readonly BindingScope _scope;
        private readonly string[] _parts;
        private readonly List<(INotifyPropertyChanged Model, PropertyChangedEventHandler Handler)> _subscriptions = new();
        private readonly bool _observe;
        private readonly bool _command;
        internal event Action? Changed;
        internal Path(BindingScope scope, string path, bool observe, bool command = false)
        {
            _scope = scope; _parts = path.Split('.'); _observe = observe; _command = command;
            if (_parts.Length < 2 || _parts.Any(string.IsNullOrEmpty)) throw new ArgumentException("Use Source.Member paths.", nameof(path));
            scope.SourcesChanged += Refresh;
            try { Refresh(); } catch { Dispose(); throw; }
        }
        internal (IMvvmObject? Owner, string Member) Resolve(bool command = false)
        {
            object? value = _scope.Get(_parts[0]);
            for (int i = 1; i < _parts.Length - 1; i++)
            {
                if (value == null) return (null, _parts[_parts.Length - 1]);
                value = ((IMvvmObject)value).Read(_parts[i]);
            }
            return ((IMvvmObject?)value, _parts[_parts.Length - 1]);
        }
        internal object? Read() { var (owner, member) = Resolve(); return owner?.Read(member); }
        private void Refresh()
        {
            foreach (var sub in _subscriptions) sub.Model.PropertyChanged -= sub.Handler;
            _subscriptions.Clear(); object? value = _scope.Get(_parts[0]);
            for (int i = 1; i < _parts.Length && value != null; i++)
            {
                var model = (IMvvmObject)value; var member = _parts[i];
                if (_observe)
                {
                    if (i < _parts.Length - 1 && !model.Describe().Single(m => m.Name == member).Observable) throw new InvalidOperationException("Reactive path requires FieldNotify: " + member);
                    if (i < _parts.Length - 1 || (!_command && model.Describe().Single(m => m.Name == member).Observable))
                    {
                        PropertyChangedEventHandler handler = (_, e) => { if (e.PropertyName == member || string.IsNullOrEmpty(e.PropertyName)) Refresh(); };
                        model.PropertyChanged += handler; _subscriptions.Add((model, handler));
                    }
                }
                if (i < _parts.Length - 1) value = model.Read(member);
            }
            Changed?.Invoke();
        }
        public void Dispose()
        { _scope.SourcesChanged -= Refresh; foreach (var sub in _subscriptions) sub.Model.PropertyChanged -= sub.Handler; _subscriptions.Clear(); }
    }
    private sealed class PropertyBinding : IDisposable
    {
        private readonly BindingScope _scope; private readonly Path _path; private readonly IBindingTarget _target; private readonly BindingOptions _options;
        private bool _updating; private bool _disposed;
        internal PropertyBinding(BindingScope scope, string path, IBindingTarget target, BindingOptions options)
        {
            _scope = scope; _target = target; _options = options; _path = new Path(scope, path, options.Mode != BindingMode.OneTime);
            try
            {
                if (options.Mode is BindingMode.OneWay or BindingMode.TwoWay)
                {
                    var (owner, member) = _path.Resolve();
                    if (owner != null && !owner.Describe().Single(m => m.Name == member).Observable) throw new InvalidOperationException("Reactive binding requires FieldNotify: " + path);
                    _path.Changed += Pull;
                }
                if (options.Mode is BindingMode.TwoWay or BindingMode.OneWayToViewModel) _target.Changed += Push;
                if (options.Mode == BindingMode.OneWayToViewModel) Push(); else Pull();
            }
            catch (Exception error)
            { try { Dispose(); } catch (Exception cleanup) { Exception? first = error; MvvmRuntime.Failure(ref first, cleanup); } throw; }
        }
        private void Pull()
        {
            if (_updating) return; _scope.Node.Context.Host.VerifyAccess(); _updating = true;
            try
            {
                var (owner, member) = _path.Resolve();
                object? value;
                if (owner == null)
                { if (!_options.HasNullValue) throw new InvalidOperationException("Null binding path."); value = _options.NullValue; }
                else { value = owner.Read(member); if (_options.Convert != null) value = _options.Convert(value); }
                _target.Write(value);
            }
            catch (Exception error) { _scope._runtime.Report(error, "MVVM read binding"); throw; }
            finally { _updating = false; }
        }
        private void Push()
        {
            if (_updating) return; _scope.Node.Context.Host.VerifyAccess(); _updating = true;
            try
            {
                var (owner, member) = _path.Resolve();
                if (owner == null) throw new InvalidOperationException("Cannot write through a null VM path.");
                var value = _target.Read(); if (_options.ConvertBack != null) value = _options.ConvertBack(value); owner.Write(member, value);
            }
            catch (Exception error) { _scope._runtime.Report(error, "MVVM write binding"); throw; }
            finally { _updating = false; }
            if (_options.Mode == BindingMode.TwoWay) Pull();
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _path.Dispose(); _target.Changed -= Push; _target.Dispose(); }
    }
    private sealed class CommandBinding : IDisposable
    {
        private readonly BindingScope _scope; private readonly Path _path; private readonly ICommandTarget _target; private readonly object? _parameter;
        private bool _disposed;
        internal CommandBinding(BindingScope scope, string path, ICommandTarget target, object? parameter)
        {
            _scope = scope; _parameter = parameter; _target = target; _path = new Path(scope, path, true, true);
            try { _path.Changed += Update; scope._runtime.CommandStateChanged += Update; target.Invoked += Invoke; Update(); }
            catch (Exception error)
            { try { Dispose(); } catch (Exception cleanup) { Exception? first = error; MvvmRuntime.Failure(ref first, cleanup); } throw; }
        }
        private IMvvmObject? _model;
        private void Subscribe()
        { if (_model != null) _model.PropertyChanged -= Changed; _model = _path.Resolve().Owner ?? throw new InvalidOperationException("Null command source."); _model.PropertyChanged += Changed; }
        private void Changed(object? sender, PropertyChangedEventArgs args) => Update();
        private void Update()
        {
            if (_disposed || _scope.Node.State is NodeState.Disposing or NodeState.Disposed) return;
            Subscribe(); var (vm, member) = _path.Resolve(); _target.SetEnabled(!_scope._runtime.IsRunning(vm!, member) && vm!.CanExecute(member));
        }
        private void Invoke()
        {
            var (vm, member) = _path.Resolve();
            if (_scope._runtime.IsRunning(vm!, member) || !vm!.CanExecute(member)) throw new InvalidOperationException("Command is unavailable.");
            _scope.Run(vm, member, _parameter);
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _path.Changed -= Update; _scope._runtime.CommandStateChanged -= Update; _path.Dispose(); if (_model != null) _model.PropertyChanged -= Changed; _target.Invoked -= Invoke; _target.Dispose(); }
    }
    private sealed class CollectionBinding : IDisposable
    {
        private readonly Path _path; private readonly ICollectionTarget _target; private readonly bool _oneTime;
        private INotifyCollectionChanged? _collection;
        private bool _disposed;
        internal CollectionBinding(BindingScope scope, string path, ICollectionTarget target, bool oneTime)
        { _target = target; _oneTime = oneTime; _path = new Path(scope, path, !oneTime); try { _path.Changed += Reload; Reload(); } catch (Exception error) { try { Dispose(); } catch (Exception cleanup) { Exception? first = error; MvvmRuntime.Failure(ref first, cleanup); } throw; } }
        private void Reload()
        {
            if (_collection != null) _collection.CollectionChanged -= Changed;
            var items = _path.Read() as IEnumerable ?? throw new InvalidOperationException("Collection source is null or not enumerable.");
            _collection = items as INotifyCollectionChanged;
            if (!_oneTime && _collection == null) throw new InvalidOperationException("Dynamic collections require INotifyCollectionChanged.");
            _target.Clear(); foreach (var item in items) _target.Insert(int.MaxValue, item);
            if (!_oneTime) _collection!.CollectionChanged += Changed;
        }
        private void Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add: for (int i = 0; i < e.NewItems!.Count; i++) _target.Insert(e.NewStartingIndex + i, e.NewItems[i]!); break;
                case NotifyCollectionChangedAction.Remove: for (int i = 0; i < e.OldItems!.Count; i++) _target.Remove(e.OldStartingIndex); break;
                case NotifyCollectionChangedAction.Move:
                    for (int i = 0; i < e.OldItems!.Count; i++)
                        _target.Move(e.OldStartingIndex < e.NewStartingIndex ? e.OldStartingIndex : e.OldStartingIndex + i,
                            e.OldStartingIndex < e.NewStartingIndex ? e.NewStartingIndex + e.OldItems.Count - 1 : e.NewStartingIndex + i);
                    break;
                case NotifyCollectionChangedAction.Replace: for (int i = 0; i < e.OldItems!.Count; i++) _target.Remove(e.OldStartingIndex); for (int i = 0; i < e.NewItems!.Count; i++) _target.Insert(e.NewStartingIndex + i, e.NewItems[i]!); break;
                case NotifyCollectionChangedAction.Reset: Reload(); break;
            }
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _path.Changed -= Reload; _path.Dispose(); if (_collection != null) _collection.CollectionChanged -= Changed; _target.Dispose(); }
    }
}
