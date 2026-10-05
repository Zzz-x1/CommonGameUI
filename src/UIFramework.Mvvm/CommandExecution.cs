namespace UIFramework.Mvvm;

internal sealed class CommandExecution : IDisposable
{
    private static readonly AsyncLocal<CommandExecution?> Current = new();
    private readonly TaskCompletionSource<Task> _completion = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CommandExecution? _parent;
    private readonly CancellationTokenRegistration _nodeLifetime;
    internal BindingScope Scope { get; }
    internal Task Completion { get; }
    internal CancellationToken Token => _cancellation.Token;
    internal CommandExecution(BindingScope scope)
    {
        Scope = scope; _parent = Current.Value; Completion = _completion.Task.Unwrap();
        // Inspect the caller that cancels the node, not the execution context that registered this callback.
        using (ExecutionContext.SuppressFlow())
            _nodeLifetime = scope.Node.Lifetime.Register(() => { if (!IsCaller) Cancel(); });
    }
    internal bool IsCaller
    {
        get
        {
            for (var caller = Current.Value; caller != null; caller = caller._parent)
                if (ReferenceEquals(this, caller) && !Completion.IsCompleted) return true;
            return false;
        }
    }
    internal void Start(Func<Task> action) => _completion.SetResult(Run(action));
    private async Task Run(Func<Task> action)
    {
        var previous = Current.Value; Current.Value = this;
        try { await action(); }
        finally { Current.Value = previous; Dispose(); }
    }
    internal void Cancel() { if (!Completion.IsCompleted) _cancellation.Cancel(); }
    public void Dispose() { _nodeLifetime.Dispose(); _cancellation.Dispose(); }
}
