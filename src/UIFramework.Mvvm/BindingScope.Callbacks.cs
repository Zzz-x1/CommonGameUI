using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace UIFramework.Mvvm;

internal sealed partial class BindingScope
{
    internal IDisposable BindCallback(string[] paths, Type[] types, Action<object?[]>? apply, BindingUpdateTiming timing,
        IBindingTarget? target = null, Type? resultType = null, Func<object?[], object?>? project = null,
        Action<CallbackBinding>? connect = null, bool writeBack = false)
    {
        var binding = new CallbackBinding(this, paths, types, apply, target, project, writeBack, timing);
        try
        {
            Node.Context.Host.VerifyAccess();
            if (_dispose != null) throw new ObjectDisposedException(nameof(BindingScope));
            Own(binding);
            binding.Initialize(resultType);
            if (!binding.Disposed) connect?.Invoke(binding);
            return binding;
        }
        catch (Exception error)
        {
            try { binding.Dispose(); } catch (Exception cleanup) { UIFailure.AddSecondary(error, cleanup); }
            binding.Report(error);
            throw;
        }
    }

    internal sealed class CallbackBinding : PropertyConnection
    {
        private sealed class ModelWatch(PropertyChangedEventHandler handler)
        {
            internal readonly PropertyChangedEventHandler Handler = handler;
            internal int References = 1;
        }

        private readonly Path[] _paths;
        private readonly int[] _firstInput;
        private readonly Type[] _types;
        private readonly object?[] _values;
        private readonly Action<object?[]>? _apply;
        private readonly IBindingTarget? _target;
        private readonly Func<object?[], object?>? _project;
        private readonly bool _writeBack;
        private Action? _disconnectInput;
        private readonly Dictionary<IMvvmObject, ModelWatch> _models = new(MvvmRuntime.ReferenceComparer.Instance);
        private bool _initialized;

        internal CallbackBinding(BindingScope scope, string[] paths, Type[] types, Action<object?[]>? apply, IBindingTarget? target,
            Func<object?[], object?>? project, bool writeBack, BindingUpdateTiming timing) : base(scope, paths, timing)
        {
            _paths = new Path[paths.Length];
            _firstInput = new int[paths.Length];
            for (int i = 0; i < paths.Length; i++) _firstInput[i] = Array.IndexOf(paths, paths[i]);
            _types = types;
            _values = new object?[paths.Length];
            _apply = apply;
            _target = target;
            _project = project;
            _writeBack = writeBack;
        }

        internal void Initialize(Type? resultType)
        {
            ValidateTiming();
            if (resultType != null)
            {
                if (_target == null) throw new ArgumentNullException("target");
                if (!_target.ValueType.IsAssignableFrom(resultType))
                    throw new InvalidCastException($"Binding result {resultType.FullName} cannot be assigned to {_target.ValueType.FullName}.");
            }
            Scope.SourceChanged += SourceReplaced;
            for (int i = 0; i < _paths.Length; i++)
            {
                if (_firstInput[i] != i) continue;
                var path = new Path(Scope, SourcePaths[i], observe: true, group: this,
                    deferWatch: Timing == BindingUpdateTiming.EndOfFrame);
                if (Disposed) { path.Dispose(); return; }
                _paths[i] = path;
            }
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
                Scope.Node.Context.Host.VerifyAccess();
                bool changed = false;
                Exception? first = null;
                foreach (var path in _paths)
                {
                    if (path == null) continue;
                    try { changed |= model == null ? path.SourceReplaced(source!) : path.MemberChanged(model, member); }
                    catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
                }
                if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
                if (changed) RequestRefresh();
            }
            catch (Exception error)
            {
                Report(error);
                throw;
            }
        }
        private (IMvvmObject Owner, MvvmMember Member)? ResolveMember(int index, bool reconnect = false)
        {
            var (owner, name) = _paths[index].Resolve(reconnect);
            if (!Active) return null;
            var path = SourcePaths[index];
            if (owner == null) throw new InvalidOperationException("Null binding path: " + path);
            var member = Scope._runtime.Plans.Member(owner, name, MvvmMemberKind.Property);
            if (!Active) return null;
            if (!member.Observable) throw new InvalidOperationException("Reactive binding requires FieldNotify: " + path);
            if (_writeBack && !member.Writable)
                throw new InvalidOperationException("Binding requires a writable property: " + path);
            if (_writeBack && !member.Type.IsAssignableFrom(_types[index]))
                throw new InvalidCastException($"Binding '{path}' cannot accept input of type {_types[index].FullName}.");
            for (int slot = index; slot < _types.Length; slot++)
                if (_firstInput[slot] == index && !_types[slot].IsAssignableFrom(member.Type))
                    throw new InvalidCastException($"Binding '{path}' provides {member.Type.FullName}, expected {_types[slot].FullName}.");
            return (owner, member);
        }
        internal void WriteValue<T>(T value)
        {
            if (ResolveMember(0) is { } resolved) resolved.Owner.Write(resolved.Member.Name, value);
        }
        internal void ConnectInput<T>(Func<Action<T>, Action> subscribe, Action<T> commit)
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            if (commit == null) throw new ArgumentNullException(nameof(commit));
            // A control may synchronously publish its draft while attaching the listener.
            var disconnect = subscribe(value =>
            {
                if (_disconnectInput != null) OnEdit(value, commit);
            }) ?? throw new InvalidOperationException("An input subscription must return an unsubscribe action.");
            if (Disposed) disconnect();
            else _disconnectInput = disconnect;
        }
        private void OnEdit<T>(T value, Action<T> commit)
        {
            if (Paused || Disposed || Updating) return;
            try
            {
                Scope.Node.Context.Host.VerifyAccess();
                Updating = true;
                Exception? failure = null;
                try { commit(value); }
                catch (Exception error) { failure = error; }
                finally { Updating = false; }
                // Even a failed edit can have changed the VM. Keep the original failure if rendering also fails.
                try { RequestRefresh(); } catch (Exception error) { MvvmRuntime.Failure(ref failure, error); }
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
            catch (Exception error)
            {
                Scope._runtime.Report(error, $"MVVM input {Scope.Node.Name}: {string.Join(", ", SourcePaths)}");
                throw;
            }
        }
        protected override void ApplyCurrent()
        {
            for (int i = 0; i < _paths.Length && !Disposed && !Paused; i++)
            {
                if (_firstInput[i] == i)
                {
                    if (ResolveMember(i, reconnect: true) is not { } resolved) return;
                    _values[i] = resolved.Owner.Read(resolved.Member.Name);
                }
                else _values[i] = _values[_firstInput[i]];
            }
            if (!Active) return;
            if (_project == null) _apply!(_values);
            else
            {
                var result = _project(_values);
                if (Active) _target!.Write(result);
            }
        }
        protected override void Disconnect()
        {
            Scope.SourceChanged -= SourceReplaced;
            Exception? first = null;
            var disconnect = _disconnectInput;
            _disconnectInput = null;
            try { disconnect?.Invoke(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            foreach (var path in _paths)
                try { path?.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            try { _target?.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
            if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
        }
    }
}
