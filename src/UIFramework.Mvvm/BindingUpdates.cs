using System.Runtime.ExceptionServices;

namespace UIFramework.Mvvm;

public sealed partial class MvvmRuntime
{
    private LinkedList<BindingScope.PropertyConnection> _pendingBindings = new();
    private LinkedList<BindingScope.PropertyConnection> _frameBindings = new();
    private bool _flushing;

    internal void Schedule(LinkedListNode<BindingScope.PropertyConnection> entry)
    {
        if (entry.List == null && _dispose == null) _pendingBindings.AddLast(entry);
    }

    /// <summary>Present pending property bindings once at the host's UI frame boundary.</summary>
    public void FlushBindings()
    {
        if (_flushing) throw new InvalidOperationException("Cannot reenter the MVVM frame flush.");
        if (_pendingBindings.Count == 0) return;
        (_frameBindings, _pendingBindings) = (_pendingBindings, _frameBindings);
        _flushing = true;
        Exception? first = null;
        try
        {
            while (_frameBindings.First is { } entry)
            {
                // Remove before applying: another notification for this binding now belongs to the next frame.
                _frameBindings.RemoveFirst();
                try { entry.Value.Refresh(); }
                catch (Exception error) { entry.Value.Report(error); Failure(ref first, error); }
            }
        }
        finally { _flushing = false; }
        if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
    }
}

internal sealed partial class BindingScope
{
    internal abstract class PropertyConnection : IDisposable
    {
        protected readonly BindingScope Scope;
        protected readonly BindingUpdateTiming Timing;
        private LinkedListNode<PropertyConnection>? _entry;
        protected bool Updating;
        internal IReadOnlyList<string> SourcePaths { get; }
        internal bool Disposed { get; private set; }
        internal bool Paused { get; set; }
        protected bool Active => !Disposed && !Paused;

        protected PropertyConnection(BindingScope scope, IReadOnlyList<string> paths, BindingUpdateTiming timing)
        {
            Scope = scope;
            SourcePaths = paths;
            Timing = timing;
        }
        protected void ValidateTiming()
        {
            if (Timing != BindingUpdateTiming.Immediate && Timing != BindingUpdateTiming.EndOfFrame)
                throw new ArgumentOutOfRangeException("timing", Timing, "Unknown binding update timing.");
        }

        protected void RequestRefresh()
        {
            if (!Active) return;
            Scope.Node.Context.Host.VerifyAccess();
            if (Timing == BindingUpdateTiming.Immediate) Refresh();
            else Scope._runtime.Schedule(_entry ??= new LinkedListNode<PropertyConnection>(this));
        }
        internal void CancelPending() => _entry?.List?.Remove(_entry);
        internal void Refresh()
        {
            if (!Active || Updating) return;
            Updating = true;
            try
            {
                Scope.Node.Context.Host.VerifyAccess();
                ApplyCurrent();
            }
            finally { Updating = false; }
        }
        internal void Report(Exception error) =>
            Scope._runtime.Report(error, $"MVVM binding {Scope.Node.Name}: {string.Join(", ", SourcePaths)}");
        protected abstract void ApplyCurrent();
        protected abstract void Disconnect();
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            CancelPending();
            if (Scope._dispose == null) Scope._bindings.Remove(this);
            Disconnect();
        }
    }
}
