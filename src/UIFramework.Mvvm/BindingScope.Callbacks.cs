using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace UIFramework.Mvvm;

internal sealed partial class BindingScope
{
    internal CallbackBinding BindCallback(string[] paths, Type[] types, Action<object?[]> apply,
        IBindingTarget? target = null, Type? resultType = null)
    {
        var binding = new CallbackBinding(this, paths, types, apply, target);
        try
        {
            Node.Context.Host.VerifyAccess();
            if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
            Own(binding);
            binding.Initialize(resultType);
            return binding;
        }
        catch (Exception error)
        {
            try { binding.Dispose(); } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
            _runtime.Report(error, $"Bind {Node.Name}: {string.Join(", ", paths)}");
            throw;
        }
    }

    internal sealed partial class CallbackBinding : IPropertyBinding
    {
        private sealed class ModelWatch(PropertyChangedEventHandler handler)
        {
            internal readonly PropertyChangedEventHandler Handler = handler;
            internal int References = 1;
        }

        private readonly BindingScope _scope;
        private readonly Path[] _paths;
        private readonly Type[] _types;
        private readonly object?[] _values;
        private readonly Action<object?[]> _apply;
        private readonly IBindingTarget? _target;
        private readonly Dictionary<IMvvmObject, ModelWatch> _models = new(MvvmRuntime.ReferenceComparer.Instance);
        private bool _initialized;
        private bool _updating;

        public IReadOnlyList<string> SourcePaths { get; }
        public bool Paused { get; set; }
        public bool Disposed { get; private set; }

        internal CallbackBinding(BindingScope scope, string[] paths, Type[] types, Action<object?[]> apply, IBindingTarget? target)
        {
            _scope = scope;
            SourcePaths = paths;
            _paths = new Path[paths.Length];
            _types = types;
            _values = new object?[paths.Length];
            _apply = apply;
            _target = target;
        }

        internal void Initialize(Type? resultType)
        {
            if (resultType != null)
            {
                if (_target == null) throw new ArgumentNullException("target");
                if (!_target.ValueType.IsAssignableFrom(resultType))
                    throw new InvalidCastException($"Binding result {resultType.FullName} cannot be assigned to {_target.ValueType.FullName}.");
            }
            _scope.SourceChanged += SourceReplaced;
            for (int i = 0; i < _paths.Length; i++)
                _paths[i] = new Path(_scope, SourcePaths[i], observe: true, group: this);
            _initialized = true;
            Refresh();
        }

        // Paths may share a source, an intermediate owner or the exact same leaf.
        // Reconnect all matching paths before rendering once for this notification.
        internal void WatchModel(IMvvmObject model)
        {
            if (_models.TryGetValue(model, out var watch))
            {
                watch.References++;
                return;
            }
            watch = new ModelWatch((_, args) => OnChanged(null, model, args.PropertyName));
            model.PropertyChanged += watch.Handler;
            _models.Add(model, watch);
        }
        internal void UnwatchModel(IMvvmObject model)
        {
            var watch = _models[model];
            if (--watch.References != 0) return;
            _models.Remove(model);
            model.PropertyChanged -= watch.Handler;
        }
        private void SourceReplaced(string source) => OnChanged(source, null, null);
        private void OnChanged(string? source, IMvvmObject? model, string? member)
        {
            if (Disposed || !_initialized) return;
            try
            {
                _scope.Node.Context.Host.VerifyAccess();
                bool changed = false;
                Exception? first = null;
                foreach (var path in _paths)
                {
                    try { changed |= model == null ? path.SourceReplaced(source!) : path.MemberChanged(model, member); }
                    catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
                }
                if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
                if (changed) Refresh();
            }
            catch (Exception error)
            {
                _scope._runtime.Report(error, $"MVVM binding {string.Join(", ", SourcePaths)}");
                throw;
            }
        }
        private (IMvvmObject Owner, MvvmMember Member) ResolveMember(int index)
        {
            var (owner, name) = _paths[index].Resolve();
            var path = SourcePaths[index];
            if (owner == null) throw new InvalidOperationException("Null binding path: " + path);
            var member = _scope._runtime.Plans.Member(owner, name, MvvmMemberKind.Property);
            if (!member.Observable) throw new InvalidOperationException("Reactive binding requires FieldNotify: " + path);
            if (!_types[index].IsAssignableFrom(member.Type))
                throw new InvalidCastException($"Binding '{path}' provides {member.Type.FullName}, expected {_types[index].FullName}.");
            return (owner, member);
        }
        public void Refresh()
        {
            if (Disposed || Paused || _updating) return;
            _updating = true;
            try
            {
                for (int i = 0; i < _paths.Length; i++)
                {
                    var (owner, member) = ResolveMember(i);
                    _values[i] = owner.Read(member.Name);
                }
                if (!Disposed) _apply(_values);
            }
            finally { _updating = false; }
        }
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            _scope.SourceChanged -= SourceReplaced;
            if (_scope._dispose == null) _scope._bindings.Remove(this);
            Exception? first = null;
            foreach (var path in _paths)
                try { path?.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            try { _target?.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
        }
    }
}
