using System.Runtime.ExceptionServices;

namespace UIFramework;

// A load has one owner (the context); callers hold independent reservations.
// Its outcome is retained explicitly so cancellation cannot leave an unobserved task.
internal sealed class UIResources
{
    private sealed class Entry
    {
        public Entry(string path) => Path = path;
        public readonly string Path;
        public int Holds;
        public Task Completion = Task.CompletedTask;
        public IUIResource? Resource;
        public ExceptionDispatchInfo? Failure;
    }
    private readonly UIContext _context;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<Entry> _pending = new();
    private readonly List<Entry> _failures = new();
    private readonly HashSet<UIResourceLease> _leases = new();
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    public UIResources(UIContext context) => _context = context;

    public async Task<UIResourceLease> AcquireAsync(string path, CancellationToken token)
    {
        _context.Host.VerifyAccess(); _context.VerifyMutation();
        if (_disposed || _context.Root.State != NodeState.Ready) throw new ObjectDisposedException(nameof(UIContext));
        token.ThrowIfCancellationRequested();
        if (!_entries.TryGetValue(path, out var entry))
        {
            entry = new Entry(path) { Holds = 1 };
            _entries.Add(path, entry); _pending.Add(entry);
            entry.Completion = LoadAsync(entry);
        }
        else entry.Holds++;
        try
        {
            await WaitAsync(entry.Completion, token);
            if (entry.Failure != null) { _failures.Remove(entry); entry.Failure.Throw(); }
            token.ThrowIfCancellationRequested();
            if (_disposed) throw new OperationCanceledException(_lifetime.Token);
            UIResourceLease lease = null!;
            lease = new UIResourceLease(_context, entry.Resource!, () => { _leases.Remove(lease); Release(entry); });
            _leases.Add(lease);
            return lease;
        }
        catch (Exception ex)
        {
            _context.Report(ex, $"Acquire {path}");
            await _context.CleanupAsync(ex, () => { Release(entry); return Task.CompletedTask; });
            throw;
        }
    }

    private async Task LoadAsync(Entry entry)
    {
        try
        {
            entry.Resource = await _context.Loader.LoadAsync(entry.Path, _lifetime.Token)
                ?? throw new InvalidOperationException($"Loader returned no resource for '{entry.Path}'.");
        }
        catch (Exception ex)
        {
            entry.Failure = ExceptionDispatchInfo.Capture(ex);
            if (!(ex is OperationCanceledException && _lifetime.IsCancellationRequested))
            { _context.Report(ex, $"Load {entry.Path}"); _failures.Add(entry); }
        }
        finally
        {
            _pending.Remove(entry);
            if (entry.Failure != null) _entries.Remove(entry.Path);
            if (entry.Holds == 0)
            {
                try { ReleaseResource(entry); }
                catch (Exception ex)
                { _context.Report(ex, $"Release {entry.Path}"); entry.Failure = ExceptionDispatchInfo.Capture(ex); _failures.Add(entry); }
            }
        }
    }

    private void Release(Entry entry)
    {
        entry.Holds--;
        if (entry.Holds == 0 && !_pending.Contains(entry)) ReleaseResource(entry);
    }
    private void ReleaseResource(Entry entry)
    {
        if (_entries.TryGetValue(entry.Path, out var current) && ReferenceEquals(current, entry)) _entries.Remove(entry.Path);
        var resource = entry.Resource; entry.Resource = null; resource?.Dispose();
    }

    private static async Task WaitAsync(Task task, CancellationToken token)
    {
        if (task.IsCompleted || !token.CanBeCanceled) { token.ThrowIfCancellationRequested(); await task; return; }
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (token.Register(() => canceled.TrySetResult(true)))
        {
            await Task.WhenAny(task, canceled.Task);
            token.ThrowIfCancellationRequested();
            await task;
        }
    }

    public async Task DisposeAsync()
    {
        _disposed = true;
        Exception? first = null;
        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception ex)
            { _context.Report(ex, "Dispose resources"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex); }
        }
        Attempt(_lifetime.Cancel);
        await Task.WhenAll(_pending.Select(e => e.Completion).ToArray());
        foreach (var lease in _leases.ToArray()) Attempt(lease.Dispose);
        foreach (var entry in _failures) Attempt(() => entry.Failure!.Throw());
        _failures.Clear(); _lifetime.Dispose();
        if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
    }
}
