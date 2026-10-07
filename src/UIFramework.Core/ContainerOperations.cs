using System.Runtime.ExceptionServices;

namespace UIFramework;

internal sealed class PresentationSession(Guid containerId)
{
    internal readonly Guid ContainerId = containerId;
    internal bool Ended;
    internal Task? EndTask, CloseTask;
}

/// <summary>UI-thread FIFO, with request-local completion and active-call dependencies.</summary>
internal sealed class ContainerOperations(ContainerNode owner)
{
    internal abstract class Request
    {
        internal UINode.OperationFrame? Caller = UINode.CurrentOperation;
        internal ExecutionContext? Context = ExecutionContext.Capture()
            ?? throw new InvalidOperationException("Container operations require execution context flow.");
        internal readonly HashSet<Request> Dependencies = new();
        internal Request? Predecessor;
        internal readonly CancellationTokenSource Cancellation;
        internal readonly CancellationToken Token;
        internal CancellationTokenRegistration Registration;
        internal volatile bool Started;
        internal abstract Task Completion { get; }
        internal abstract Task InvokeAsync();
        internal abstract void Fail(Exception error);
        internal abstract void Complete(Exception? error);
        protected Request(CancellationTokenSource cancellation) { Cancellation = cancellation; Token = cancellation.Token; }
        internal void Release()
        {
            Registration.Dispose(); Cancellation.Dispose();
            for (var frame = Caller; frame != null; frame = frame.Previous) frame.Request?.Dependencies.Remove(this);
            Dependencies.Clear(); Predecessor = null; Caller = null; Context = null;
        }
    }
    private sealed class Result<T>(T value, Exception? error)
    { internal readonly T Value = value; internal readonly Exception? Error = error; }
    private sealed class Request<T> : Request
    {
        private readonly Func<CancellationToken, Task<T>> _action;
        private readonly TaskCompletionSource<Result<T>> _result = new();
        private readonly Task<T> _completion;
        private T _value = default!;
        internal Request(Func<CancellationToken, Task<T>> action, CancellationTokenSource cancellation) : base(cancellation)
        { _action = action; _completion = WaitAsync(); }
        internal override Task Completion => _completion;
        internal Task<T> Task => _completion;
        private async Task<T> WaitAsync()
        {
            var result = await _result.Task;
            if (result.Error != null) ExceptionDispatchInfo.Capture(result.Error).Throw();
            return result.Value;
        }
        internal override async Task InvokeAsync() { Token.ThrowIfCancellationRequested(); _value = await _action(Token); }
        internal override void Complete(Exception? error) => _result.TrySetResult(new Result<T>(_value, error));
        internal override void Fail(Exception error) => _result.TrySetResult(new Result<T>(default!, error));
    }
    private static readonly AsyncLocal<Request?> Executing = new();
    internal static Request? ExecutingRequest => Executing.Value;
    private readonly Queue<Request> _pending = new();
    private Request? _active;
    private bool _draining;
    private Exception? _failure;
    private TaskCompletionSource<bool>? _idle;
    private Task? _drain;
    internal bool IsBusy => _draining;
    internal Task CurrentTask => _active!.Completion;
    internal Task WhenIdle => _draining ? _idle!.Task : _drain ?? Task.CompletedTask;

    internal void Block(Exception failure) => _failure ??= failure;
    internal void Verify(bool allowCached = false)
    {
        owner.Context.Host.VerifyAccess(); owner.Context.VerifyMutation();
        if (_failure != null) ExceptionDispatchInfo.Capture(_failure).Throw();
        for (UINode? node = owner; node != null; node = node.Parent)
            if (node.State is NodeState.Disposing or NodeState.Disposed)
                throw new ObjectDisposedException(node.Name);
        if (owner.State != NodeState.Ready || owner.IsCached && !allowCached)
            throw new InvalidOperationException($"Container {owner.Name} is not available.");
        for (var frame = UINode.CurrentOperation; frame != null; frame = frame.Previous)
            if (frame.Active)
                for (var node = frame.Node; node != null; node = node.Parent)
                    if (ReferenceEquals(node, owner))
                        throw new InvalidOperationException($"Reentrant structural operation on {owner.Name} from an active lifecycle or operation.");
    }
    internal Task<T> Enqueue<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token, bool allowCached)
    {
        Request<T>? request = null;
        try
        {
            Verify(allowCached); token.ThrowIfCancellationRequested();
            var tokens = new List<CancellationToken> { token };
            for (UINode? node = owner; node != null; node = node.Parent) tokens.Add(node.Lifetime);
            request = new Request<T>(action, CancellationTokenSource.CreateLinkedTokenSource(tokens.ToArray()));
            request.Predecessor = _pending.LastOrDefault() ?? _active;
            var callers = new HashSet<Request>();
            for (var frame = request.Caller; frame != null; frame = frame.Previous)
                if (frame.Active && frame.Request is { } caller && !caller.Completion.IsCompleted) callers.Add(caller);
            foreach (var caller in callers)
            {
                if (Reaches(request, caller, new HashSet<Request>()))
                    throw new InvalidOperationException($"Circular container operation dependency while requesting {operation} on {owner.Name}.");
                caller.Dependencies.Add(request);
            }
            var queued = request;
            request.Registration = request.Token.Register(() =>
            { if (!queued.Started) queued.Fail(new OperationCanceledException(queued.Token)); });
            _pending.Enqueue(request);
            if (!_draining)
            { _draining = true; _idle = new TaskCompletionSource<bool>(); _drain = DrainAsync(); }
            return request.Task;
        }
        catch (Exception error)
        {
            owner.ReportFailure(error, $"Submit {operation} {owner.Name}");
            if (request != null) { request.Fail(error); request.Release(); return request.Task; }
            return ThrowAsync<T>(error);
        }
    }
    private static async Task<T> ThrowAsync<T>(Exception error)
    { await Task.CompletedTask; ExceptionDispatchInfo.Capture(error).Throw(); return default!; }
    private static bool Reaches(Request request, Request target, HashSet<Request> visited)
    {
        if (!visited.Add(request)) return false;
        if (request.Completion.IsCompleted)
            return request.Predecessor != null && Reaches(request.Predecessor, target, visited);
        if (ReferenceEquals(request, target)) return true;
        if (request.Predecessor != null && Reaches(request.Predecessor, target, visited)) return true;
        return request.Dependencies.Any(next => Reaches(next, target, visited));
    }
    // Requests observe their own outcome; disposal also observes an unexpected drain failure.
    private async Task DrainAsync()
    {
        var idle = _idle!;
        try
        {
            while (_pending.Count != 0)
            {
                var request = _pending.Dequeue();
                if (request.Completion.IsCompleted) { request.Release(); continue; }
                _active = request; request.Started = true;
                Exception? error = null;
                try
                {
                    await InvokeWithContext(request);
                }
                catch (Exception failure) { error = failure; owner.ReportFailure(failure, $"Queued operation {owner.Name}"); }
                finally { _active = null; request.Release(); }
                // Release an empty queue before resuming callers, so a follow-up request
                // can start immediately with its own input lock in the same frame.
                var emptied = _pending.Count == 0;
                if (emptied) { _draining = false; idle.TrySetResult(true); }
                request.Complete(error);
                if (emptied) return;
            }
        }
        catch (Exception error)
        {
            Block(error); owner.ReportFailure(error, $"Drain operations {owner.Name}");
            _active?.Fail(error);
            while (_pending.Count != 0) { var request = _pending.Dequeue(); request.Fail(error); request.Release(); }
            idle.TrySetException(error);
            throw;
        }
        finally { if (ReferenceEquals(_idle, idle)) _draining = false; idle.TrySetResult(true); }
    }
    private Task InvokeWithContext(Request request)
    {
        Task invocation = null!;
        ExecutionContext.Run(request.Context!, _ =>
        { Executing.Value = request; invocation = InvokeAsync(request); }, null);
        return invocation;
    }
    private async Task InvokeAsync(Request request)
    { VerifyForExecution(); await request.InvokeAsync(); }
    private void VerifyForExecution()
    {
        if (_failure != null) ExceptionDispatchInfo.Capture(_failure).Throw();
        for (UINode? node = owner; node != null; node = node.Parent)
            if (node.State is NodeState.Disposing or NodeState.Disposed) throw new OperationCanceledException();
    }
}
