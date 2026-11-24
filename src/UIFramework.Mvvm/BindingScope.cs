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
    private readonly List<Task> _tasks = new();
    private readonly List<Task> _initialCollections = new();
    private readonly List<CommandExecution> _commands = new();
    private IViewBindingConfiguration? _configuration;
    private Task? _dispose;
    private Task? _release;
    private event Action<string>? SourceChanged;
    internal UINode Node { get; }
    public BindingScope(UINode node, MvvmRuntime runtime, IViewModelAssets? assets = null)
    { Node = node; _runtime = runtime; _assets = assets; runtime.Add(this); }
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
    internal void ApplyRows(IViewBindingSlots slots, bool preview, Action<IBindingTarget>? captureProperty, Action<object>? captureCollection)
    {
        Node.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
        int start = _bindings.Count;
        for (int slot = 0; slot < slots.Count; slot++)
        {
            BindingRule rule = default;
            object? native = null;
            try
            {
                if (!slots.IsEnabled(slot)) continue;
                rule = slots.GetRule(slot);
                native = slots.GetTarget(slot);
                var plan = _runtime.Plans.Resolve(rule, native.GetType());
                switch (rule.Kind)
                {
                    case ViewBindingKind.Property:
                        var target = plan.Property!.CreateTarget(native, rule.Mode);
                        try { captureProperty?.Invoke(target); }
                        catch (Exception error)
                        {
                            try { target.Dispose(); } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
                            throw;
                        }
                        Own(new PropertyBinding(this, rule.Source, target, plan.Options!, preview, plan));
                        break;
                    case ViewBindingKind.Command:
                        if (!slots.CommandTargetType.IsInstanceOfType(native))
                            throw new InvalidCastException("Command target must be " + slots.CommandTargetType.FullName);
                        Own(new CommandBinding(this, rule.Source, preview ? null : slots.CreateCommandTarget(native), plan.Command!));
                        break;
                    case ViewBindingKind.Collection:
                        captureCollection?.Invoke(native);
                        Collection(rule.Source, slots.CreateCollectionTarget(Node, slot, preview), rule.OneTimeCollection);
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(rule.Kind));
                }
            }
            catch (Exception error)
            {
                _runtime.Report(error, $"Bind {Node.Name} row {slot}: {rule.Source} -> {native?.GetType().Name}.{rule.Member}");
                for (int i = _bindings.Count - 1; i >= start; i--)
                {
                    try { _bindings[i].Dispose(); } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
                    _bindings.RemoveAt(i);
                }
                throw;
            }
        }
    }
    internal async Task InitializeAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ApplyConfiguration();
        var canceled = new TaskCompletionSource<bool>();
        using (token.Register(() => canceled.TrySetResult(true)))
        {
            while (_initialCollections.Count != 0)
            {
                _initialCollections.RemoveAll(task => task.IsCompletedSuccessfully);
                if (_initialCollections.Count == 0) break;
                var completed = await Task.WhenAny(_initialCollections.Append(canceled.Task));
                token.ThrowIfCancellationRequested();
                await completed;
                _initialCollections.Remove(completed);
            }
        }
        token.ThrowIfCancellationRequested();
    }
    public void Set(string name, object value)
    {
        Node.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
        if (_assets is IViewModelAssetPolicy policy) policy.ValidateBorrowed(value, _runtime.OwnsShared(value));
        if (value is not IMvvmObject) throw new InvalidOperationException("VM must be woven: " + value.GetType().FullName);
        _sources[name] = value; SourceChanged?.Invoke(name);
    }
    public object Get(string name, Type? requestedType = null)
    {
        Node.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
        var request = $"node '{Node.Name}' ({Node.Owner.GetType().FullName}), source '{name}', requested type '{requestedType?.FullName ?? typeof(object).FullName}'";
        if (!_sources.TryGetValue(name, out var value))
        {
            if (name == "Item") throw new KeyNotFoundException($"Missing collection-injected VM source 'Item'; requested by {request}.");
            value = _runtime.GetGlobalViewModel<object>(name, request);
        }
        if (requestedType != null && !requestedType.IsInstanceOfType(value))
            throw new InvalidCastException($"VM source '{name}' has type '{value.GetType().FullName}'; requested by {request}.");
        return value;
    }
    public T Create<T>(string name, object? template) where T : class
    {
        Node.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
        if (_sources.ContainsKey(name)) throw new ArgumentException("Duplicate VM source: " + name);
        var value = (T)_runtime.Create(typeof(T), template, _assets, false, (item, adapter) => _owned.Add((item, adapter)));
        _sources.Add(name, value); SourceChanged?.Invoke(name); return value;
    }
    public IDisposable Bind(string path, IBindingTarget target, BindingOptions options) => Own(new PropertyBinding(this, path, target, options));
    public void PauseBinding(string propertyName) => ChangeBindingState(propertyName, true);
    public void ResumeBinding(string propertyName) => ChangeBindingState(propertyName, false);
    private void ChangeBindingState(string propertyName, bool pause)
    {
        try
        {
            Node.Context.Host.VerifyAccess();
            if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("A VM property name or source path is required.", nameof(propertyName));
            var bindings = _bindings.OfType<PropertyBinding>().Where(binding => !binding.Disposed && binding.HasSource &&
                (propertyName.Contains('.') ? binding.SourcePath == propertyName : binding.Member == propertyName)).ToArray();
            if (bindings.Length == 0) throw new KeyNotFoundException("No property binding: " + propertyName);
            if (bindings.Select(binding => binding.SourcePath).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new InvalidOperationException("Ambiguous property binding; use a complete Source.Member path: " + propertyName);
            if (bindings.Any(binding => binding.Paused == pause))
                throw new InvalidOperationException(pause ? "Property binding is already paused: " + propertyName : "Property binding is not paused: " + propertyName);
            foreach (var binding in bindings) binding.Paused = pause;
            if (!pause)
            {
                Exception? first = null;
                foreach (var binding in bindings)
                    try { binding.Refresh(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
                if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
            }
        }
        catch (Exception error) { _runtime.Report(error, pause ? "MVVM pause binding" : "MVVM resume binding"); throw; }
    }
    public IDisposable Command(string path, ICommandTarget target, object? parameter) => Own(new CommandBinding(this, path, target, parameter));
    public IDisposable Command(string path, ICommandTarget target, CommandBindingOptions options) => Own(new CommandBinding(this, path, target, options));
    public IDisposable Collection(string path, ICollectionTarget target, bool oneTime) => Own(new CollectionBinding(this, path, target, oneTime));
    private T Own<T>(T binding) where T : IDisposable { _bindings.Add(binding); return binding; }
    internal void Observe(Task task) { RemoveCompletedTasks(); _tasks.Add(task); _runtime.Observe(task); }
    internal void ObserveCollection(Task task)
    {
        Observe(task);
        if (Node.State == NodeState.Initializing) _initialCollections.Add(task);
    }
    internal void Add(CommandExecution command) => _commands.Add(command);
    internal void RemoveCompletedTasks() => _tasks.RemoveAll(task => task.IsCompletedSuccessfully);
    internal void Remove(CommandExecution command)
    {
        _commands.Remove(command);
        // Preserve failures for cleanup independently of active command tracking.
        if (!command.Completion.IsCompletedSuccessfully) _tasks.Add(command.Completion);
    }
    internal bool HasCaller => _commands.Any(command => command.IsCaller);
    internal Task Run(IMvvmObject vm, string command, object? parameter) => _runtime.Execute(this, vm, command, parameter);
    public ValueTask DisposeAsync()
    {
        if (_dispose == null)
        {
            var closed = new TaskCompletionSource<Task>();
            var released = new TaskCompletionSource<Task>();
            _dispose = closed.Task.Unwrap(); _release = released.Task.Unwrap();
            if (HasCaller) _runtime.ObserveRelease(_release);
            closed.SetResult(CloseAsync()); released.SetResult(ReleaseAsync());
        }
        return new ValueTask(HasCaller ? _dispose : _release!);
    }
    private async Task CloseAsync()
    {
        Exception? first = null;
        foreach (var binding in _bindings.AsEnumerable().Reverse())
            try { binding.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        var others = _commands.Where(command => !command.IsCaller).ToArray();
        foreach (var command in others)
            try { command.Cancel(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        first = await MvvmRuntime.WaitForTasksAsync(others.Select(command => command.Completion).Concat(_tasks), first);
        _sources.Clear(); _bindings.Clear();
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    private async Task ReleaseAsync()
    {
        Exception? first = null;
        try { await _dispose!; } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        first = await MvvmRuntime.WaitForTasksAsync(_commands.Select(command => command.Completion), first);
        foreach (var value in _owned.AsEnumerable().Reverse())
            try { await _runtime.Release(value.Value, value.Assets); } catch (Exception error) { _runtime.Report(error, "MVVM VM release"); MvvmRuntime.Failure(ref first, error); }
        _runtime.Remove(this); _owned.Clear(); _commands.Clear(); _tasks.Clear(); _initialCollections.Clear();
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    internal sealed class Path : IDisposable
    {
        private readonly BindingScope _scope;
        private readonly string[] _parts;
        private readonly List<(INotifyPropertyChanged Model, PropertyChangedEventHandler Handler)> _subscriptions = new();
        private readonly bool _observe;
        private readonly bool _command;
        private bool _disposed;
        internal event Action? Changed;
        internal Path(BindingScope scope, string path, bool observe, bool command = false)
        {
            _scope = scope; _parts = scope._runtime.Plans.Path(path); _observe = observe; _command = command;
            scope.SourceChanged += OnSourceChanged;
            try { if (_observe) Watch(1, scope.Get(_parts[0])); } catch { Dispose(); throw; }
        }
        internal (IMvvmObject? Owner, string Member) Resolve()
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
        private void OnSourceChanged(string name)
        {
            if (_disposed || name != _parts[0]) return;
            if (_observe) Watch(1, _scope.Get(name));
            Changed?.Invoke();
        }
        private void Watch(int start, object? value)
        {
            if (_subscriptions.Count >= start && ReferenceEquals(value, _subscriptions[start - 1].Model)) return;
            UnsubscribeFrom(start);
            for (int i = start; i < _parts.Length && value != null; i++)
            {
                var model = (IMvvmObject)value;
                var member = _parts[i];
                var subscription = i - 1;
                var next = i + 1;
                var leaf = next == _parts.Length;
                if (leaf && _command) break;
                if (!_scope._runtime.Plans.Member(model, member, MvvmMemberKind.Property).Observable)
                {
                    if (!leaf) throw new InvalidOperationException("Reactive path requires FieldNotify: " + member);
                    break;
                }
                PropertyChangedEventHandler handler = (_, e) =>
                {
                    if (_subscriptions.Count <= subscription || !ReferenceEquals(_subscriptions[subscription].Model, model)) return;
                    if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != member) return;
                    if (!leaf) Watch(next, model.Read(member));
                    Changed?.Invoke();
                };
                model.PropertyChanged += handler;
                _subscriptions.Add((model, handler));
                if (!leaf) value = model.Read(member);
            }
        }
        private void UnsubscribeFrom(int firstPart)
        {
            for (var i = _subscriptions.Count - 1; i >= firstPart - 1; i--)
            {
                var (model, handler) = _subscriptions[i];
                model.PropertyChanged -= handler;
                _subscriptions.RemoveAt(i);
            }
        }
        public void Dispose()
        { _disposed = true; _scope.SourceChanged -= OnSourceChanged; UnsubscribeFrom(1); }
    }
    private sealed class PropertyBinding : IDisposable
    {
        private readonly BindingScope _scope; private readonly Path? _path; private readonly IBindingTarget _target; private readonly BindingOptions _options;
        private bool _updating; private bool _disposed;
        private readonly CompiledBinding? _plan;
        private Type? _validatedOwner;
        internal string SourcePath { get; }
        internal string Member => SourcePath.Substring(SourcePath.LastIndexOf('.') + 1);
        internal bool HasSource => !_options.HasConstantValue;
        internal bool Disposed => _disposed;
        internal bool Paused { get; set; }
        internal void Refresh() => Pull();
        internal PropertyBinding(BindingScope scope, string path, IBindingTarget target, BindingOptions options, bool preview = false, CompiledBinding? plan = null)
        {
            _scope = scope; _target = target; _options = options; _plan = plan; SourcePath = path;
            try
            {
                if (options.HasConstantValue && options.Mode is BindingMode.TwoWay or BindingMode.OneWayToViewModel)
                    throw new InvalidOperationException("Constant input cannot be written back to a ViewModel.");
                _path = options.HasConstantValue ? null : new Path(scope, path, options.Mode != BindingMode.OneTime);
                if (_path != null)
                {
                    var (owner, member) = _path.Resolve();
                    if (owner != null) ValidateMember(owner, member);
                    else if (options.Mode == BindingMode.OneWayToViewModel)
                        throw new InvalidOperationException("Cannot write through a null VM path.");
                    if (options.Mode is BindingMode.OneWay or BindingMode.TwoWay)
                        _path.Changed += Pull;
                }
                if (!preview && options.Mode is (BindingMode.TwoWay or BindingMode.OneWayToViewModel)) _target.Changed += Push;
                if (options.Mode == BindingMode.OneWayToViewModel)
                { if (!preview) Push(); }
                else Pull();
            }
            catch (Exception error)
            { try { Dispose(); } catch (Exception cleanup) { Exception? first = error; MvvmRuntime.Failure(ref first, cleanup); } throw; }
        }
        private void ValidateMember(IMvvmObject owner, string name)
        {
            var ownerType = owner.GetType();
            if (ownerType == _validatedOwner || _plan?.ValidatedOwners.Contains(ownerType) == true) return;
            var member = _scope._runtime.Plans.Member(owner, name, MvvmMemberKind.Property);
            if (!member.Observable && _options.Mode is BindingMode.OneWay or BindingMode.TwoWay)
                throw new InvalidOperationException("Reactive binding requires FieldNotify: " + SourcePath);
            if (_options.Mode is BindingMode.TwoWay or BindingMode.OneWayToViewModel)
            {
                if (!member.Writable) throw new InvalidOperationException("Binding requires a writable property: " + SourcePath);
                ValidateConversion(_target.ValueType, member.Type, _options.ConvertBack, _options.BackwardConverter);
            }
            if (_options.Mode != BindingMode.OneWayToViewModel)
                ValidateConversion(member.Type, _target.ValueType, _options.Convert, _options.ForwardConverter);
            _validatedOwner = ownerType;
            _plan?.ValidatedOwners.Add(ownerType);
        }
        private void ValidateConversion(Type from, Type to, Func<object?, object?>? convert, ConverterInfo? definition)
        {
            // Code bindings may provide an arbitrary delegate; its own invocation checks its contract.
            if (definition == null && convert != null) return;
            if (definition != null && !definition.Input.IsAssignableFrom(from) || !to.IsAssignableFrom(definition?.Output ?? from))
                throw new InvalidCastException($"Binding {SourcePath} cannot convert {from.FullName} to {to.FullName}.");
        }
        private void Pull()
        {
            _scope.Node.Context.Host.VerifyAccess();
            if (_updating || Paused) return;
            _updating = true;
            try
            {
                if (_options.HasConstantValue)
                {
                    var constant = _options.ConstantValue;
                    _target.Write(_options.Convert == null ? constant : _options.Convert(constant));
                    return;
                }
                var (owner, member) = _path!.Resolve();
                object? value;
                if (owner == null)
                { if (!_options.HasNullValue) throw new InvalidOperationException("Null binding path."); value = _options.NullValue; }
                else { ValidateMember(owner, member); value = owner.Read(member); if (_options.Convert != null) value = _options.Convert(value); }
                _target.Write(value);
            }
            catch (Exception error) { _scope._runtime.Report(error, "MVVM read binding"); throw; }
            finally { _updating = false; }
        }
        private void Push()
        {
            _scope.Node.Context.Host.VerifyAccess();
            if (_updating || Paused) return;
            _updating = true;
            try
            {
                var (owner, member) = _path!.Resolve();
                if (owner == null) throw new InvalidOperationException("Cannot write through a null VM path.");
                ValidateMember(owner, member);
                var value = _target.Read(); if (_options.ConvertBack != null) value = _options.ConvertBack(value); owner.Write(member, value);
            }
            catch (Exception error) { _scope._runtime.Report(error, "MVVM write binding"); throw; }
            finally { _updating = false; }
            if (_options.Mode == BindingMode.TwoWay) Pull();
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _path?.Dispose(); _target.Changed -= Push; _target.Dispose(); }
    }
    private sealed class CommandBinding : IDisposable
    {
        private readonly BindingScope _scope; private readonly Path _path; private readonly ICommandTarget? _target; private object? _parameter;
        private readonly Path? _parameterPath;
        private readonly CommandBindingOptions _options;
        private Type? _parameterType;
        private bool _disposed;
        internal CommandBinding(BindingScope scope, string path, ICommandTarget target, object? parameter)
            : this(scope, path, target, new CommandBindingOptions { Mode = parameter == null ? CommandParameterMode.None : CommandParameterMode.Constant, ConstantValue = parameter }) { }
        internal CommandBinding(BindingScope scope, string path, ICommandTarget? target, CommandBindingOptions options)
        {
            _scope = scope; _target = target; _options = options;
            try
            {
                if (options == null) throw new ArgumentNullException(nameof(options));
                _path = new Path(scope, path, true, true);
                if (options.Mode == CommandParameterMode.SourcePath) _parameterPath = new Path(scope, options.ParameterPath, false);
                else _parameter = options.ConstantValue;
                var (vm, member) = _path.Resolve();
                if (vm == null) throw new InvalidOperationException("Null command source.");
                ValidateSignature(vm, member);
                if (target == null) { _path.Dispose(); _parameterPath?.Dispose(); return; }
                _path.Changed += Update; scope._runtime.CommandStateChanged += Update; target.Invoked += Invoke; Update();
            }
            catch (Exception error)
            { try { Dispose(); } catch (Exception cleanup) { Exception? first = error; MvvmRuntime.Failure(ref first, cleanup); } throw; }
        }
        private void ValidateSignature(IMvvmObject vm, string member)
        {
            var type = _scope._runtime.Plans.Member(vm, member, MvvmMemberKind.Command).Type;
            if (type == _parameterType) return;
            if (_options.Mode == CommandParameterMode.None && type != typeof(void))
                throw new ArgumentException("Command requires a business parameter.");
            if (_parameterPath != null)
            {
                var (owner, name) = _parameterPath.Resolve();
                if (owner == null) throw new InvalidOperationException("Null command parameter source.");
                var property = _scope._runtime.Plans.Member(owner, name, MvvmMemberKind.Property);
                if (!type.IsAssignableFrom(property.Type)) throw new InvalidCastException("Command parameter source type mismatch.");
            }
            else
            {
                if (_options.Mode == CommandParameterMode.Constant && _options.ConstantText != null)
                    _parameter = CommandParameters.ParseConstant(_options.ConstantText, type);
                CommandParameters.Validate(type, _parameter);
            }
            _parameterType = type;
        }
        private IMvvmObject? _model;
        private void Changed(object? sender, PropertyChangedEventArgs args) => Update();
        private void Update()
        {
            if (_disposed || _scope.Node.State is NodeState.Disposing or NodeState.Disposed) return;
            var (vm, member) = _path.Resolve();
            if (vm == null) throw new InvalidOperationException("Null command source.");
            if (!ReferenceEquals(_model, vm))
            {
                ValidateSignature(vm, member);
                if (_model != null) _model.PropertyChanged -= Changed;
                _model = vm;
                _model.PropertyChanged += Changed;
            }
            _target!.SetEnabled(!_scope._runtime.IsRunning(vm, member) && vm.CanExecute(member));
        }
        private void Invoke()
        {
            try
            {
                var (vm, member) = _path.Resolve();
                if (_scope._runtime.IsRunning(vm!, member) || !vm!.CanExecute(member)) throw new InvalidOperationException("Command is unavailable.");
                var parameter = _parameterPath == null ? _parameter : _parameterPath.Read();
                CommandParameters.Validate(_scope._runtime.Plans.Member(vm, member, MvvmMemberKind.Command).Type, parameter);
                _scope.Run(vm, member, parameter);
            }
            catch (Exception error) { _scope._runtime.Report(error, "MVVM command parameter"); throw; }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            if (_path != null) { _path.Changed -= Update; _path.Dispose(); }
            _scope._runtime.CommandStateChanged -= Update;
            _parameterPath?.Dispose(); if (_model != null) _model.PropertyChanged -= Changed;
            if (_target != null) { _target.Invoked -= Invoke; _target.Dispose(); }
        }
    }
    private sealed class CollectionBinding : IDisposable
    {
        private readonly BindingScope _scope; private readonly Path _path; private readonly ICollectionTarget _target; private readonly bool _oneTime;
        private IEnumerable? _items;
        private bool _disposed;
        internal CollectionBinding(BindingScope scope, string path, ICollectionTarget target, bool oneTime)
        { _scope = scope; _target = target; _oneTime = oneTime; try { _path = new Path(scope, path, !oneTime); _path.Changed += Reload; Reload(); } catch (Exception error) { try { Dispose(); } catch (Exception cleanup) { Exception? first = error; MvvmRuntime.Failure(ref first, cleanup); } throw; } }
        private void Reload()
        {
            var items = _path.Read() as IEnumerable ?? throw new InvalidOperationException("Collection source is null or not enumerable.");
            if (ReferenceEquals(items, _items)) return;
            if (!_oneTime && items is not INotifyCollectionChanged) throw new InvalidOperationException("Dynamic collections require INotifyCollectionChanged.");
            if (!_oneTime && _items is INotifyCollectionChanged old) old.CollectionChanged -= Changed;
            _items = items;
            Rebuild(items);
            if (!_oneTime) ((INotifyCollectionChanged)items).CollectionChanged += Changed;
        }
        private void Rebuild(IEnumerable items)
        {
            _target.Clear();
            foreach (var item in items) _target.Insert(int.MaxValue, item);
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
                case NotifyCollectionChangedAction.Reset: Rebuild(_items!); break;
            }
        }
        public void Dispose() { if (_disposed) return; _disposed = true; if (_path != null) { _path.Changed -= Reload; _path.Dispose(); } if (!_oneTime && _items is INotifyCollectionChanged collection) collection.CollectionChanged -= Changed; _items = null; if (_target is IAsyncDisposable asyncTarget) _scope.Observe(asyncTarget.DisposeAsync().AsTask()); else _target.Dispose(); }
    }
}
