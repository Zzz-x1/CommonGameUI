using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace UIFramework.Mvvm;

internal sealed partial class BindingScope : IAsyncDisposable
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
                        BindProperty(rule.Source, target, plan.Options!, preview, plan);
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
                    var binding = _bindings[i];
                    _bindings.RemoveAt(i);
                    try { binding.Dispose(); } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
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
    public IDisposable Bind(string path, IBindingTarget target, BindingOptions options) => BindProperty(path, target, options);
    private IDisposable BindProperty(string path, IBindingTarget target, BindingOptions options, bool preview = false, CompiledBinding? plan = null)
    {
        var binding = new PropertyBinding(this, path, target, options, plan);
        try
        {
            Node.Context.Host.VerifyAccess();
            if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
            Own(binding);
            binding.Initialize(preview);
            return binding;
        }
        catch (Exception error)
        {
            try { binding.Dispose(); } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
            if (plan == null) binding.Report(error); // Serialized rows report the slot and target at their caller.
            throw;
        }
    }
    public void PauseBinding(string propertyName) => ChangeBindingState(propertyName, true);
    public void ResumeBinding(string propertyName) => ChangeBindingState(propertyName, false);
    private void ChangeBindingState(string propertyName, bool pause)
    {
        Exception? first = null;
        try
        {
            Node.Context.Host.VerifyAccess();
            if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("A VM property name or source path is required.", nameof(propertyName));
            var matches = (from binding in _bindings.OfType<PropertyConnection>()
                           where !binding.Disposed
                           from path in binding.SourcePaths
                           where path == propertyName || path.EndsWith("." + propertyName, StringComparison.Ordinal) && !propertyName.Contains('.')
                           select (binding, path)).ToArray();
            if (matches.Length == 0) throw new KeyNotFoundException("No property binding: " + propertyName);
            if (matches.Any(match => match.path != matches[0].path))
                throw new InvalidOperationException("Ambiguous property binding; use a complete Source.Member path: " + propertyName);
            var bindings = matches.Select(match => match.binding).Distinct().ToArray();
            if (bindings.Any(binding => binding.Paused == pause))
                throw new InvalidOperationException(pause ? "Property binding is already paused: " + propertyName : "Property binding is not paused: " + propertyName);
            foreach (var binding in bindings)
            {
                binding.CancelPending();
                binding.Paused = pause;
            }
            if (!pause)
            {
                foreach (var binding in bindings)
                    try { binding.CancelPending(); binding.Refresh(); }
                    catch (Exception error) { binding.Report(error); MvvmRuntime.Failure(ref first, error); }
            }
        }
        catch (Exception error) { _runtime.Report(error, pause ? "MVVM pause binding" : "MVVM resume binding"); throw; }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
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
        private readonly List<(IMvvmObject Model, PropertyChangedEventHandler? Handler)> _subscriptions = new();
        private readonly CallbackBinding? _group;
        private readonly bool _observe;
        private readonly bool _command;
        private readonly bool _deferWatch;
        private bool _disposed;
        internal event Action? Changed;
        internal Path(BindingScope scope, string path, bool observe, bool command = false, CallbackBinding? group = null, bool deferWatch = false)
        {
            _scope = scope; _parts = scope._runtime.Plans.Path(path); _observe = observe; _command = command; _group = group; _deferWatch = deferWatch;
            if (group == null) scope.SourceChanged += OnSourceChanged;
            try { if (_observe && !deferWatch) Watch(1, scope.Get(_parts[0])); }
            catch (Exception error)
            {
                try { Dispose(); } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
                throw;
            }
        }
        internal (IMvvmObject? Owner, string Member) Resolve(bool reconnect = false)
        {
            object? value = _scope.Get(_parts[0]);
            for (int i = 1; i < _parts.Length && value != null && !_disposed; i++)
            {
                var model = (IMvvmObject)value;
                if (reconnect && _deferWatch && !_disposed &&
                    (i > _subscriptions.Count || !ReferenceEquals(_subscriptions[i - 1].Model, model)))
                {
                    UnsubscribeFrom(i);
                    Subscribe(i, model);
                }
                if (!_disposed && i < _parts.Length - 1) value = model.Read(_parts[i]);
            }
            return (_disposed ? null : (IMvvmObject?)value, _parts[_parts.Length - 1]);
        }
        internal object? Read() { var (owner, member) = Resolve(); return owner?.Read(member); }
        private void OnSourceChanged(string name)
        {
            if (SourceReplaced(name)) Changed?.Invoke();
        }
        internal bool SourceReplaced(string name)
        {
            if (_disposed || name != _parts[0]) return false;
            if (_deferWatch) UnsubscribeFrom(1);
            else if (_observe) Watch(1, _scope.Get(name));
            return true;
        }
        private bool MemberChanged(int index, IMvvmObject model, string? property)
        {
            if (_disposed || index >= _subscriptions.Count || !ReferenceEquals(_subscriptions[index].Model, model)) return false;
            var member = _parts[index + 1];
            if (!string.IsNullOrEmpty(property) && property != member) return false;
            if (index + 2 < _parts.Length)
            {
                if (_deferWatch) UnsubscribeFrom(index + 2);
                else Watch(index + 2, model.Read(member));
            }
            return true;
        }
        internal bool MemberChanged(IMvvmObject model, string? property)
        {
            bool changed = false;
            for (int i = 0; i < _subscriptions.Count; i++) changed |= MemberChanged(i, model, property);
            return changed;
        }
        private void Watch(int start, object? value)
        {
            if (_subscriptions.Count >= start && ReferenceEquals(value, _subscriptions[start - 1].Model)) return;
            UnsubscribeFrom(start);
            for (int i = start; i < _parts.Length && value != null && !_disposed; i++)
            {
                var model = (IMvvmObject)value;
                Subscribe(i, model);
                if (!_disposed && i + 1 < _parts.Length) value = model.Read(_parts[i]);
            }
        }
        private void Subscribe(int part, IMvvmObject model)
        {
            var member = _parts[part];
            var leaf = part + 1 == _parts.Length;
            if (leaf && _command) return;
            if (!_scope._runtime.Plans.Member(model, member, MvvmMemberKind.Property).Observable)
            {
                if (!leaf) throw new InvalidOperationException("Reactive path requires FieldNotify: " + member);
                return;
            }
            if (_disposed) return;
            PropertyChangedEventHandler? handler = null;
            if (_group == null)
            {
                handler = (_, e) => { if (MemberChanged(part - 1, model, e.PropertyName)) Changed?.Invoke(); };
                model.PropertyChanged += handler;
            }
            else _group.WatchModel(model);
            if (_disposed) Unsubscribe(model, handler);
            else _subscriptions.Add((model, handler));
        }
        private void Unsubscribe(IMvvmObject model, PropertyChangedEventHandler? handler)
        {
            if (_group == null) model.PropertyChanged -= handler;
            else _group.UnwatchModel(model);
        }
        private void UnsubscribeFrom(int firstPart)
        {
            Exception? first = null;
            for (var i = _subscriptions.Count - 1; i >= firstPart - 1; i--)
            {
                var (model, handler) = _subscriptions[i];
                _subscriptions.RemoveAt(i);
                try { Unsubscribe(model, handler); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
        public void Dispose()
        { _disposed = true; _scope.SourceChanged -= OnSourceChanged; UnsubscribeFrom(1); }
    }
    private sealed class PropertyBinding : PropertyConnection
    {
        private Path? _path;
        private readonly IBindingTarget _target;
        private readonly BindingOptions _options;
        private readonly CompiledBinding? _plan;
        private Type? _validatedOwner;
        private bool _inputConnected;
        private string SourcePath { get; }
        internal PropertyBinding(BindingScope scope, string path, IBindingTarget target, BindingOptions options, CompiledBinding? plan)
            : base(scope, options.HasConstantValue ? Array.Empty<string>() : new[] { path }, options.UpdateTiming)
        {
            _target = target; _options = options; _plan = plan; SourcePath = path;
        }
        internal void Initialize(bool preview)
        {
            ValidateTiming();
            var mode = _options.Mode;
            bool twoWay = mode == BindingMode.TwoWay;
            if (_options.HasConstantValue && (twoWay || mode == BindingMode.OneWayToViewModel))
                throw new InvalidOperationException("Constant input cannot be written back to a ViewModel.");
            if (!_options.HasConstantValue)
            {
                bool observesValues = twoWay || mode == BindingMode.OneWay;
                var path = new Path(Scope, SourcePath, mode != BindingMode.OneTime,
                    deferWatch: observesValues && Timing == BindingUpdateTiming.EndOfFrame);
                if (Disposed) { path.Dispose(); return; }
                _path = path;
                if (observesValues) path.Changed += SourceUpdated;
            }
            if (mode != BindingMode.OneWayToViewModel) Refresh();
            else if (!preview) WriteCurrent();
            else
            {
                var (owner, member) = _path!.Resolve();
                ValidateMember(owner ?? throw new InvalidOperationException("Cannot write through a null VM path."), member);
            }
            if (!Disposed && !preview && (twoWay || mode == BindingMode.OneWayToViewModel))
            {
                Updating = true;
                try
                {
                    try { _target.Changed += Push; }
                    catch (Exception error)
                    {
                        try { _target.Changed -= Push; } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
                        throw;
                    }
                    if (Disposed) _target.Changed -= Push;
                    else _inputConnected = true;
                }
                finally { Updating = false; }
            }
        }
        private void SourceUpdated()
        {
            try { RequestRefresh(); }
            catch (Exception error) { Report(error); throw; }
        }
        private void ValidateMember(IMvvmObject owner, string name)
        {
            var ownerType = owner.GetType();
            if (ownerType == _validatedOwner || _plan?.ValidatedOwners.Contains(ownerType) == true) return;
            var member = Scope._runtime.Plans.Member(owner, name, MvvmMemberKind.Property);
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
        protected override void ApplyCurrent()
        {
            object? value = _options.ConstantValue;
            if (!_options.HasConstantValue)
            {
                var (owner, member) = _path!.Resolve(reconnect: true);
                if (!Active) return;
                if (owner == null)
                {
                    if (!_options.HasNullValue) throw new InvalidOperationException("Null binding path.");
                    _target.Write(_options.NullValue);
                    return;
                }
                ValidateMember(owner, member);
                if (!Active) return;
                value = owner.Read(member);
            }
            if (Active && _options.Convert != null) value = _options.Convert(value);
            if (Active) _target.Write(value);
        }
        private void Push()
        {
            try { WriteCurrent(); }
            catch (Exception error) { Report(error); throw; }
        }
        private void WriteCurrent()
        {
            if (!Active || Updating) return;
            Scope.Node.Context.Host.VerifyAccess();
            Updating = true;
            Exception? first = null;
            try
            {
                var (owner, member) = _path!.Resolve();
                if (!Active) return;
                if (owner == null) throw new InvalidOperationException("Cannot write through a null VM path.");
                ValidateMember(owner, member);
                if (!Active) return;
                var value = _target.Read();
                if (Active && _options.ConvertBack != null) value = _options.ConvertBack(value);
                if (Active) owner.Write(member, value);
            }
            catch (Exception error) { first = error; }
            finally { Updating = false; }
            if (_options.Mode == BindingMode.TwoWay)
                try { RequestRefresh(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
        protected override void Disconnect()
        {
            Exception? first = null;
            try { _path?.Dispose(); } catch (Exception error) { first = error; }
            if (_inputConnected)
                try { _target.Changed -= Push; } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            try { _target.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
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
