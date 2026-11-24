namespace UIFramework;

public abstract class ContainerNode : UINode
{
    private readonly Dictionary<Type, UINode> _cache = new();
    private ContainerOperations? _operations;
    private ContainerOperations Operations => _operations ??= new ContainerOperations(this);
    internal bool HasPendingOperations => _operations?.IsBusy == true;
    internal void BlockOperations(Exception failure) => Operations.Block(failure);
    internal override Task WaitForOperationsAsync() => _operations?.WhenIdle ?? Task.CompletedTask;
    protected void CommitState(Action apply)
    { try { apply(); } catch (Exception error) { BlockOperations(error); throw; } }
    // Subclasses decide membership and visibility; the operation driver projects
    // the same stable records on commit, interrupted preparation and native loss.
    protected abstract bool IsPresented(UINode child);
    private protected void ApplyPresentation(UINode? child)
    {
        if (child?.State != NodeState.Ready) return;
        var shown = IsPresented(child);
        child.SetLocalState(shown, shown);
    }
    private protected Task RecoverPresentationAsync(Exception primary, UINode? child) =>
        Context.CleanupAsync(primary, () => { CommitState(() => ApplyPresentation(child)); return Task.CompletedTask; });
    private protected void RestoreBackdrop(IUIBackdrop backdrop, bool visible)
    { try { backdrop.Restore(visible); } catch (Exception error) { BlockOperations(error); throw; } }
    private protected void RestoreBackdropOrder(IUIBackdrop backdrop, object order)
    { try { backdrop.RestoreOrder(order); } catch (Exception error) { BlockOperations(error); throw; } }

    protected Task CloseInstanceAsync(IUINode owner, Func<UINode, CancellationToken, Task> close, CancellationToken token)
    {
        try
        {
            Operations.Verify(); token.ThrowIfCancellationRequested();
            var node = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
            var session = node.Presentation;
            if (session == null || session.ContainerId != Id) throw new InvalidOperationException("The instance does not belong to this container.");
            if (session.Ended) return WaitForCloseAsync(session.EndTask!, token);
            if (session.CloseTask is { IsCompleted: false } pending) return WaitForCloseAsync(pending, token);
            var completion = new TaskCompletionSource<Task>();
            session.CloseTask = completion.Task.Unwrap();
            completion.SetResult(OperateAsync("Close", async ct =>
            {
                if (session.Ended) await session.EndTask!;
                else
                {
                    if (!ReferenceEquals(node.Presentation, session)) throw new InvalidOperationException("The target opening has been replaced.");
                    await close(node, ct);
                }
                return true;
            }, token));
            return session.CloseTask;
        }
        catch (Exception error) { ReportFailure(error, $"Close request {Name}"); return FailedCloseAsync(error); }
    }
    private static async Task FailedCloseAsync(Exception error)
    { await Task.CompletedTask; System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw(); }
    private static async Task WaitForCloseAsync(Task close, CancellationToken token)
    {
        if (!token.CanBeCanceled) { await close; return; }
        var canceled = new TaskCompletionSource<bool>();
        using (token.Register(() => canceled.TrySetResult(true)))
        {
            await Task.WhenAny(close, canceled.Task); token.ThrowIfCancellationRequested(); await close;
        }
    }
    private protected PresentationSession CaptureOpening(IUINode owner)
    {
        var node = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
        var session = node.Presentation;
        if (session == null || session.ContainerId != Id) throw new InvalidOperationException("The instance does not belong to this container.");
        return session;
    }
    private protected static void RequireOpening(UINode node, PresentationSession session)
    {
        if (!ReferenceEquals(node.Presentation, session) || session.Ended) throw new InvalidOperationException("The target opening is no longer available.");
    }

    private protected Task OperateOpeningAsync(string operation, IUINode owner,
        Func<UINode, CancellationToken, Task<bool>> action, CancellationToken token)
    {
        try
        {
            var session = CaptureOpening(owner);
            var node = owner.Node;
            return OperateAsync(operation, ct =>
            { RequireOpening(node, session); return action(node, ct); }, token);
        }
        catch (Exception error) { ReportFailure(error, operation); return FailedCloseAsync(error); }
    }
    protected void MarkClosed(UINode node)
    {
        if (node.Presentation is { Ended: false } session) session.End(Operations.CurrentTask);
    }

    protected async Task<UINode> AcquireAsync<T>(object? args, CancellationToken token) where T : class, IUINode
    {
        token.ThrowIfCancellationRequested();
        UINode node;
        if (_cache.TryGetValue(typeof(T), out node!))
        {
            _cache.Remove(typeof(T)); node.Cached = false; node.CacheReuse = true;
            try
            {
                node.SetParameters(args);
                if (node is IDialogResult dialog) dialog.BeginSession();
            }
            catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
        }
        else node = await Context.CreateAsync<T>(this, args, token);
        node.Presentation = CreateOpening(node);
        return node;
    }
    private protected virtual PresentationSession CreateOpening(UINode node) => new(this, node);

    protected async Task ReleaseClosedAsync(UINode node, bool retain = true)
    {
        MarkClosed(node);
        if (node is IDialogResult dialog) dialog.EndSession();
        node.CacheReuse = false;
        if (!retain || node.CacheMode == NodeCacheMode.None || State != NodeState.Ready)
        { await node.DisposeAsync(); return; }
        try
        {
            if (_cache.TryGetValue(node.RegisteredType!, out var previous))
            { _cache.Remove(node.RegisteredType!); await previous.DisposeAsync(); }
            node.SetLocalState(false, false); node.Cached = true;
            _cache.Add(node.RegisteredType!, node);
        }
        catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
    }

    public Task ClearCacheAsync<T>(CancellationToken cancellationToken = default) where T : class, IUINode =>
        ClearCacheOperationAsync(typeof(T), cancellationToken);
    public Task ClearCacheAsync(CancellationToken cancellationToken = default) => ClearCacheOperationAsync(null, cancellationToken);
    internal Task ClearContextCacheAsync(CancellationToken token) => ClearCacheOperationAsync(null, token, allowCached: true);
    private Task ClearCacheOperationAsync(Type? type, CancellationToken token, bool allowCached = false) =>
        OperateAsync("ClearCache", async ct =>
        {
            var nodes = _cache.Where(p => type == null || p.Key == type).Select(p => p.Value).ToArray();
            ct.ThrowIfCancellationRequested();
            Exception? first = null;
            foreach (var node in nodes)
            {
                _cache.Remove(node.RegisteredType!);
                try { await node.DisposeAsync(); }
                catch (Exception ex) { Context.Report(ex, "Clear cached node"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex); }
            }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
            return true;
        }, token, allowCached: allowCached);

    protected override void OnChildRemoved(UINode child)
    {
        if (child.RegisteredType != null && _cache.TryGetValue(child.RegisteredType, out var cached) && ReferenceEquals(cached, child))
            _cache.Remove(child.RegisteredType);
    }

    private IUITransition? _childTransition;
    public IUITransition? ChildTransition
    {
        get => _childTransition;
        set { try { if (State != NodeState.Detached) { Context.Host.VerifyAccess(); Context.VerifyMutation(); if (HasPendingOperations) throw new InvalidOperationException("Cannot configure a busy container."); } _childTransition = value; }
            catch (Exception ex) { ReportFailure(ex, "Configure child transition"); throw; } }
    }
    protected virtual void OnOperationLockChanged(bool locked) { }
    private void LockOperation(bool locked)
    {
        try { SetInputLocked(locked); OnOperationLockChanged(locked); }
        catch (Exception error) { BlockOperations(error); throw; }
    }
    protected override bool InitiallyVisible => true;

    protected Task<T> OperateAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token, Action? beforeLock = null, bool allowCached = false) =>
        Operations.Enqueue(operation, ct => ExecuteOperationAsync(operation, action, ct, beforeLock, allowCached), token, allowCached);
    private async Task<T> ExecuteOperationAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token, Action? beforeLock, bool allowCached)
    {
        T result = default!;
        await RunAsync(operation, async ct =>
        {
            Exception? primary = null;
            try { beforeLock?.Invoke(); LockOperation(true); result = await action(ct); }
            catch (Exception ex) { primary = ex; throw; }
            finally
            {
                if (State == NodeState.Ready)
                {
                    if (primary == null) LockOperation(false);
                    else await Context.CleanupAsync(primary, () => { LockOperation(false); return Task.CompletedTask; });
                }
                else
                {
                    // A disposing modal still owns a context-wide temporary input block.
                    if (primary == null) OnOperationLockChanged(false);
                    else await Context.CleanupAsync(primary, () => { OnOperationLockChanged(false); return Task.CompletedTask; });
                }
            }
        }, token, allowCached: allowCached);
        return result;
    }
    protected LifecycleContext Describe(UINode node, LifecycleReason reason, UINode? source, UINode? target) =>
        new(node, this, reason, source, target);
    protected void CompleteChange(LifecycleContext operation, UINode? exiting, LifecycleContext? exit,
        UINode? entering, LifecycleContext? enter, Action? focus = null,
        ModalContainerNode? closingModal = null, object? restoreFocus = null)
    {
        try
        {
            ApplyPresentation(exiting);
            ApplyPresentation(entering);
            if (entering != null) entering.HasEntered = true;
            LockOperation(false);
            Context.ReconcileModalInput(closingModal, restoreFocus);
            focus?.Invoke();
        }
        catch (Exception error) { BlockOperations(error); throw; }
        try
        {
            Context.PublishChanges(operation, exit);
            if (exiting != null) exiting.NotifyExited(exit!);
            if (entering != null) entering.NotifyEntered(enter!);
        }
        finally { if (entering != null) entering.CacheReuse = false; }
    }
    protected async Task FinishCloseAsync(UINode node, Action notify)
    {
        MarkClosed(node);
        try { notify(); }
        catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
        await ReleaseClosedAsync(node);
    }

    protected async Task PrepareChangeAsync(LifecycleContext? exit, LifecycleContext? enter, CancellationToken token, bool playAnimation,
        TransitionDirection direction = TransitionDirection.None, Func<CancellationToken, Task>? accompanyingAnimation = null)
    {
        var outgoing = exit?.Node.Node;
        var incoming = enter?.Node.Node;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token,
            outgoing?.Lifetime ?? default, incoming?.Lifetime ?? default);
        try
        {
            await TransitionFlow.PrepareAsync(this, exit, enter, direction, playAnimation, lifetime.Token, accompanyingAnimation);
            lifetime.Token.ThrowIfCancellationRequested();
        }
        catch (Exception error)
        {
            await RecoverPresentationAsync(error, outgoing);
            await RecoverPresentationAsync(error, incoming);
            throw;
        }
    }

}

/// <summary>Independent windows with registration-defined instance identity and retained hiding.</summary>
public class PanelContainerNode : ContainerNode
{
    private sealed class WindowRecord(PanelContainerNode container, UINode node) : PresentationSession(container, node)
    {
        public bool Shown { get; set; } = true;
    }
    private readonly Dictionary<UINode, WindowRecord> _windows = new();
    private readonly Dictionary<Type, UINode> _single = new();
    private protected override PresentationSession CreateOpening(UINode node) => new WindowRecord(this, node);
    protected override bool IsPresented(UINode child) => _windows.TryGetValue(child, out var record) && record.Shown;

    public Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true,
        CancellationToken cancellationToken = default) where T : class, IUINode =>
        OperateAsync("Open", async ct =>
        {
            ct.ThrowIfCancellationRequested();
            var supplied = args != null;
            var mode = Context.GetWindowMode<T>();
            if (mode == WindowInstanceMode.Single && _single.TryGetValue(typeof(T), out var existing))
            {
                var record = RequireWindow(existing);
                if (supplied) throw new InvalidOperationException("Creation parameters cannot be supplied to an existing single window; update it through its business API.");
                if (!record.Shown) await ShowCoreAsync(existing, record, playAnimation, ct);
                return (T)existing.Owner;
            }
            var node = await AcquireAsync<T>(args, ct);
            var enter = Describe(node, LifecycleReason.Open, null, node);
            try { await PrepareChangeAsync(null, enter, ct, playAnimation); }
            catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
            _windows.Add(node, (WindowRecord)node.Presentation!);
            if (mode == WindowInstanceMode.Single) _single.Add(typeof(T), node);
            CompleteChange(enter, null, null, node, enter);
            return (T)node.Owner;
        }, cancellationToken);

    public bool TryGet<T>([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? node) where T : class, IUINode
    {
        try
        {
            Context.Host.VerifyAccess();
            if (State != NodeState.Ready) throw new InvalidOperationException($"Window container is {State}.");
            var matches = _windows.Keys.Where(n => n.RegisteredType == typeof(T) && n.State == NodeState.Ready).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException($"Several windows of registered type {typeof(T).Name} exist; use their instance references.");
            node = matches.Length == 0 ? null : (T)matches[0].Owner;
            return node != null;
        }
        catch (Exception ex) { ReportFailure(ex, $"TryGet<{typeof(T).Name}> {Name}"); throw; }
    }

    private WindowRecord RequireWindow(UINode node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (node.Parent != this || node.State != NodeState.Ready || !_windows.TryGetValue(node, out var record))
            throw new InvalidOperationException("The instance is not an available window of this container.");
        return record;
    }

    public Task HideAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateOpeningAsync("Hide", owner, async (node, ct) =>
        {
            var record = RequireWindow(node);
            if (!record.Shown) return true;
            var exit = Describe(node, LifecycleReason.Hide, node, null);
            await PrepareChangeAsync(exit, null, ct, playAnimation);
            record.Shown = false;
            CompleteChange(exit, node, exit, null, null);
            return true;
        }, cancellationToken);

    public Task ShowAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateOpeningAsync("Show", owner, async (node, ct) =>
        {
            var record = RequireWindow(node);
            if (record.Shown) return true;
            await ShowCoreAsync(node, record, playAnimation, ct);
            return true;
        }, cancellationToken);

    private async Task ShowCoreAsync(UINode node, WindowRecord record, bool playAnimation, CancellationToken ct)
    {
        var enter = Describe(node, LifecycleReason.Show, null, node);
        await PrepareChangeAsync(null, enter, ct, playAnimation);
        record.Shown = true;
        CompleteChange(enter, null, null, node, enter);
    }

    public Task CloseAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        CloseInstanceAsync(owner, async (node, ct) =>
        {
            var record = RequireWindow(node);
            if (!record.Shown)
            {
                ct.ThrowIfCancellationRequested();
                RemoveWindow(node);
                await ReleaseClosedAsync(node);
                return;
            }
            var exit = Describe(node, LifecycleReason.Close, node, null);
            await PrepareChangeAsync(exit, null, ct, playAnimation);
            RemoveWindow(node);
            await FinishCloseAsync(node, () => CompleteChange(exit, node, exit, null, null));
        }, cancellationToken);

    private void RemoveWindow(UINode node)
    {
        if (_windows.Remove(node) && _single.TryGetValue(node.RegisteredType!, out var single) && ReferenceEquals(single, node))
            _single.Remove(node.RegisteredType!);
    }
    protected override void OnChildRemoved(UINode child) { RemoveWindow(child); base.OnChildRemoved(child); }
}

public class StackContainerNode : ContainerNode
{
    private readonly List<PresentationSession> _history = new();
    public IReadOnlyList<IUINode> History => _history.Select(entry => entry.Node.Owner).ToArray();
    public IUINode? Current => CurrentNode?.Owner;
    private UINode? CurrentNode => _history.LastOrDefault()?.Node;
    protected override bool IsPresented(UINode child) => ReferenceEquals(CurrentNode, child);

    public Task<T> PushAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode =>
        OperateAsync("Push", async ct =>
        {
            ct.ThrowIfCancellationRequested();
            if (Context.GetWindowMode<T>() == WindowInstanceMode.Single)
            {
                var index = _history.FindIndex(entry => entry.Node.RegisteredType == typeof(T));
                if (index >= 0)
                {
                    if (args != null) throw new InvalidOperationException("Creation parameters cannot be supplied to an existing single window; update it through its business API.");
                    var existing = _history[index].Node;
                    if (index != _history.Count - 1) await ReturnCoreAsync(index, playAnimation, ct);
                    return (T)existing.Owner;
                }
            }
            var node = await AcquireAsync<T>(args, ct);
            // A lifetime may end the old page while the new resource is loading.
            var previous = CurrentNode;
            var enter = Describe(node, LifecycleReason.Push, previous, node);
            var exit = previous == null ? null : Describe(previous, LifecycleReason.Covered, previous, node);
            try
            {
                await PrepareChangeAsync(exit, enter, ct, playAnimation, TransitionDirection.Forward);
            }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            _history.Add(node.Presentation!);
            CompleteChange(enter, previous, exit, node, enter);
            return (T)node.Owner;
        }, cancellationToken);

    public Task PopAsync(bool playAnimation = true, CancellationToken cancellationToken = default) => OperateAsync("Pop", async ct =>
    {
        if (CurrentNode == null) return true;
        await ReturnCoreAsync(_history.Count - 2, playAnimation, ct);
        return true;
    }, cancellationToken);

    public Task PopToAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) => OperateOpeningAsync("PopTo", owner, async (target, ct) =>
    {
        var index = _history.IndexOf(target.Presentation!);
        if (target.Parent != this || target.State != NodeState.Ready || index < 0)
            throw new InvalidOperationException("Target must be a live instance in this stack's history.");
        if (index == _history.Count - 1) return true;
        await ReturnCoreAsync(index, playAnimation, ct);
        return true;
    }, cancellationToken);

    private async Task ReturnCoreAsync(int targetIndex, bool playAnimation, CancellationToken ct)
    {
        var current = CurrentNode!;
        var previous = targetIndex < 0 ? null : _history[targetIndex].Node;
        var removed = _history.Skip(targetIndex + 1).Reverse().ToArray();
        var exit = Describe(current, LifecycleReason.Close, current, previous);
        var enter = previous == null ? null : Describe(previous, LifecycleReason.Return, current, previous);
        using var participants = CancellationTokenSource.CreateLinkedTokenSource(removed.Select(entry => entry.Node.Lifetime).Append(ct).ToArray());
        await PrepareChangeAsync(exit, enter, participants.Token, playAnimation, TransitionDirection.Backward);
        // Removing an unrelated earlier page during preparation can shift the target index.
        _history.RemoveRange(_history.Count - removed.Length, removed.Length);
        foreach (var entry in removed) MarkClosed(entry.Node);
        Exception? first = null;
        try { CompleteChange(exit, current, exit, previous, enter); }
        catch (Exception ex) { Context.Report(ex, "Return completion"); first = ex; }
        foreach (var entry in removed)
        {
            try { await ReleaseClosedAsync(entry.Node, first == null); }
            catch (Exception ex)
            {
                Context.Report(ex, "Return cleanup");
                if (first == null) first = ex; else UIFailure.AddSecondary(first, ex);
            }
        }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }

    protected override void OnChildRemoved(UINode child)
    {
        base.OnChildRemoved(child);
        var wasCurrent = CurrentNode == child;
        if (!_history.Remove(child.Presentation!) || !wasCurrent || CurrentNode is not { State: NodeState.Ready } previous) return;
        for (UINode? owner = this; owner != null; owner = owner.Parent)
            if (owner.State == NodeState.Disposing) return;

        // Ordinary pops remove history before disposal. A lifetime ending here
        // exposes the underlying page immediately, preserving any ongoing operation's lock.
        CommitState(() =>
        {
            ApplyPresentation(previous);
            Context.ReconcileModalInput();
        });
        Context.PublishChanges(Describe(previous, LifecycleReason.Return, child, previous));
    }
}

/// <summary>Switcher for existing embedded nodes. Names are ordinal and container-local.</summary>
public class TabContainerNode : ContainerNode
{
    private readonly Dictionary<string, UINode> _items = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, IUINode> Items => _items.ToDictionary(p => p.Key, p => p.Value.Owner, StringComparer.Ordinal);
    public string? CurrentName { get; private set; }
    public IUINode? Current => CurrentNode?.Owner;
    private UINode? CurrentNode => CurrentName == null ? null : _items[CurrentName];
    protected override bool IsPresented(UINode child) => ReferenceEquals(CurrentNode, child);

    public Task BindAsync(string name, IUINode owner, IUIElement? embeddedElement = null,
        CancellationToken cancellationToken = default) => OperateAsync("BindTab", async ct =>
    {
        var node = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tab name is required.", nameof(name));
        if (_items.ContainsKey(name)) throw new InvalidOperationException($"Duplicate tab name '{name}'.");
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (node.State != NodeState.Detached) throw new InvalidOperationException("Tab node is already attached.");
        await node.AttachAsync(this, name, embeddedElement, ct);
        _items.Add(name, node);
        return true;
    }, cancellationToken);

    public Task SwitchAsync(string name, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("SwitchTab", ct => SwitchCoreAsync(name, playAnimation, ct), cancellationToken);

    private async Task<bool> SwitchCoreAsync(string name, bool playAnimation, CancellationToken ct)
    {
        if (!_items.TryGetValue(name, out var next)) throw new KeyNotFoundException($"Tab '{name}' does not exist in '{Name}'.");
        if (ReferenceEquals(CurrentNode, next)) return true;
        var previous = CurrentNode;
        var enter = Describe(next, LifecycleReason.Switch, previous, next);
        var exit = previous == null ? null : Describe(previous, LifecycleReason.Switch, previous, next);
        await PrepareChangeAsync(exit, enter, ct, playAnimation);
        CurrentName = name;
        CompleteChange(enter, previous, exit, next, enter);
        return true;
    }

    public Task SwitchAsync<T>(bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode =>
        OperateAsync("SwitchTab", ct =>
        {
            var names = _items.Where(pair => pair.Value.Owner is T).Select(pair => pair.Key).ToArray();
            if (names.Length != 1) throw new InvalidOperationException($"Expected one tab of type {typeof(T).Name}, found {names.Length}; use its name.");
            return SwitchCoreAsync(names[0], playAnimation, ct);
        }, cancellationToken);

    public Task HideAsync(bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("HideTab", async ct =>
        {
            var current = CurrentNode;
            if (current == null) return true;
            var exit = Describe(current, LifecycleReason.Hide, current, null);
            await PrepareChangeAsync(exit, null, ct, playAnimation);
            CurrentName = null;
            CompleteChange(exit, current, exit, null, null);
            return true;
        }, cancellationToken);

    protected override void OnChildRemoved(UINode child)
    {
        base.OnChildRemoved(child);
        var name = _items.FirstOrDefault(pair => ReferenceEquals(pair.Value, child)).Key;
        if (name != null) { _items.Remove(name); if (CurrentName == name) CurrentName = null; }
    }
}

/// <summary>Topmost modal owns input; this container owns its configured backdrop displays.</summary>
public class ModalContainerNode : ContainerNode
{
    private sealed class ModalRecord(ModalContainerNode container, UINode node) : PresentationSession(container, node)
    {
        internal object? Focus;
        internal IUIBackdrop? Backdrop;
        // The outcome is shared with result waiters, including failed close attempts.
        public TaskCompletionSource<Exception?>? CloseAttempt;
    }
    private readonly List<ModalRecord> _modals = new();
    private protected override PresentationSession CreateOpening(UINode node) => new ModalRecord(this, node);
    protected override bool IsPresented(UINode child) =>
        child.Presentation is ModalRecord record && _modals.Contains(record);
    private IUIBackdropFactory? _backdropFactory;
    private bool _backdropDisabled;
    private IUIBackdrop? _sharedBackdrop;
    private bool _backdropLocked;
    private bool _dismissPending;
    private bool _ownsInputBlock;
    public ModalBackdropMode BackdropMode { get; private set; } = ModalBackdropMode.Shared;
    public IUIBackdrop? CurrentBackdrop => _modals.LastOrDefault()?.Backdrop;
    public IUINode? Current => CurrentNode?.Owner;
    private UINode? CurrentNode => _modals.LastOrDefault()?.Node;
    internal UINode? VisibleTop
    {
        get
        {
            if (!CommittedVisible) return null;
            for (var i = _modals.Count - 1; i >= 0; i--)
                if (_modals[i].Node is { State: NodeState.Ready, CommittedVisible: true } top) return top;
            return null;
        }
    }
    internal object? FocusBefore(UINode modal) => ((ModalRecord)modal.Presentation!).Focus;
    internal object? BackgroundFocus => _modals.FirstOrDefault()?.Focus;

    public void ConfigureBackdrop(IUIBackdropFactory factory, ModalBackdropMode mode = ModalBackdropMode.Shared)
    {
        if (factory == null)
        {
            var failure = new ArgumentNullException(nameof(factory));
            ReportFailure(failure, "Configure modal backdrop"); throw failure;
        }
        ConfigureBackdropCore(factory, false, mode);
    }
    public void DisableBackdrop() => ConfigureBackdropCore(null, true, BackdropMode);
    public void UseDefaultBackdrop(ModalBackdropMode mode = ModalBackdropMode.Shared) => ConfigureBackdropCore(null, false, mode);
    private void ConfigureBackdropCore(IUIBackdropFactory? factory, bool disabled, ModalBackdropMode mode)
    {
        try
        {
            if (!Enum.IsDefined(typeof(ModalBackdropMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (State != NodeState.Detached) { Context.Host.VerifyAccess(); Context.VerifyMutation(); }
            if (_modals.Count != 0 || HasPendingOperations || _backdropLocked || State is NodeState.Disposing or NodeState.Disposed)
                throw new InvalidOperationException("Configure backdrops before opening modals.");
            _backdropFactory = factory; _backdropDisabled = disabled; BackdropMode = mode;
        }
        catch (Exception ex) { ReportFailure(ex, "Configure modal backdrop"); throw; }
    }

    public async Task<bool> RequestDismissAsync(CancellationToken token = default)
    {
        try
        {
            Context.Host.VerifyAccess(); Context.VerifyMutation(); token.ThrowIfCancellationRequested();
            var node = CurrentNode ?? throw new InvalidOperationException("No modal is available to dismiss.");
            if (_backdropLocked || _dismissPending || !node.IsInteractive) throw new InvalidOperationException("The modal cannot receive a dismiss request now.");
            if (node.DismissRequested == null) return false; // Explicitly non-dismissible business UI.
            _dismissPending = true;
            try { await node.DismissRequested(token); }
            finally { _dismissPending = false; }
            return true;
        }
        catch (Exception ex) { Context.Report(ex, "Modal dismiss request"); throw; }
    }

    protected override void OnOperationLockChanged(bool locked)
    {
        _backdropLocked = locked;
        if (locked != _ownsInputBlock)
        {
            _ownsInputBlock = locked;
            if (locked) Context.BeginModalInputBlock(); else Context.EndModalInputBlock();
        }
        UpdateBackdropInput();
    }
    private void UpdateBackdropInput()
    {
        if (_sharedBackdrop != null)
        {
            _sharedBackdrop.SetVisible(IsVisible && _modals.Any(entry => entry.Node.IsVisible));
            _sharedBackdrop.SetInteractive(!_backdropLocked && IsInteractive && CurrentBackdrop != null);
            return;
        }
        foreach (var entry in _modals)
        {
            entry.Backdrop?.SetVisible(IsVisible && entry.Node.IsVisible);
            entry.Backdrop?.SetInteractive(!_backdropLocked && IsInteractive && ReferenceEquals(entry.Backdrop, CurrentBackdrop));
        }
    }
    protected override void OnApplyState() => UpdateBackdropInput();
    private void OrderBackdrops()
    {
        if (BackdropMode == ModalBackdropMode.Shared)
        {
            foreach (var modal in _modals.Where(entry => !ReferenceEquals(entry.Node, CurrentNode))) modal.Node.Element?.BringToFront();
            _sharedBackdrop?.BringToFront(); CurrentNode?.Element?.BringToFront();
        }
        else foreach (var modal in _modals)
        {
            modal.Backdrop?.BringToFront();
            modal.Node.Element?.BringToFront();
        }
    }

    public Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode =>
        OpenCoreAsync<T>(args, playAnimation, cancellationToken);
    private Task<T> OpenCoreAsync<T>(object? args, bool playAnimation, CancellationToken cancellationToken, Action<T, ModalRecord>? opened = null) where T : class, IUINode
    {
        object? focus = null;
        return OperateAsync("OpenModal", async ct =>
        {
            ct.ThrowIfCancellationRequested();
            if (Context.InputOwnerNode != null && Context.InputOwnerNode.Parent != this)
                throw new InvalidOperationException("Independent modal containers cannot own input simultaneously.");
            if (Context.GetWindowMode<T>() == WindowInstanceMode.Single)
            {
                var index = _modals.FindIndex(entry => entry.Node.RegisteredType == typeof(T));
                if (index >= 0)
                {
                    if (args != null) throw new InvalidOperationException("Creation parameters cannot be supplied to an existing single window; update it through its business API.");
                    var existing = _modals[index];
                    if (index != _modals.Count - 1) await CloseRangeAsync(index + 1, LifecycleReason.Return, playAnimation, ct);
                    opened?.Invoke((T)existing.Node.Owner, existing);
                    return (T)existing.Node.Owner;
                }
            }
            var backdropFactory = _backdropDisabled ? null : _backdropFactory ?? Context.DefaultBackdropFactory;
            var node = await AcquireAsync<T>(args, ct);
            var previous = CurrentNode;
            var enter = Describe(node, LifecycleReason.Open, previous, node);
            IUIBackdrop? backdrop = null;
            var createdBackdrop = false;
            object? priorOrder = null;
            try
            {
                using var participants = CancellationTokenSource.CreateLinkedTokenSource(ct, node.Lifetime, previous?.Lifetime ?? default);
                backdrop = BackdropMode == ModalBackdropMode.Shared ? _sharedBackdrop : null;
                if (backdrop != null || backdropFactory != null)
                {
                    if (backdrop == null)
                    {
                        backdrop = await backdropFactory!.CreateAsync(Element, () => RequestDismissAsync(), participants.Token)
                            ?? throw new InvalidOperationException("Backdrop factory returned null.");
                        createdBackdrop = true;
                        backdrop.SetInteractive(false);
                    }
                    else priorOrder = backdrop.CaptureOrder();
                    backdrop.BringToFront();
                    node.Element?.BringToFront();
                }
                participants.Token.ThrowIfCancellationRequested();
                await PrepareChangeAsync(null, enter, participants.Token, playAnimation, TransitionDirection.None,
                    createdBackdrop ? token => backdrop!.ShowAsync(playAnimation, token) : null);
                node.Element?.BringToFront();
                participants.Token.ThrowIfCancellationRequested();
                if (Context.InputOwnerNode != null && Context.InputOwnerNode.Parent != this)
                    throw new InvalidOperationException("Another modal container acquired input while this modal was preparing.");
            }
            catch (Exception ex)
            {
                if (backdrop != null)
                {
                    if (createdBackdrop) await Context.CleanupAsync(ex, async () => await backdrop.DisposeAsync());
                    else if (priorOrder != null) await Context.CleanupAsync(ex, () => { RestoreBackdropOrder(backdrop, priorOrder); return Task.CompletedTask; });
                }
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            var record = (ModalRecord)node.Presentation!;
            record.Focus = focus;
            record.Backdrop = backdrop;
            _modals.Add(record);
            if (_modals.Count == 1) Context.TrackModal(this, true);
            if (BackdropMode == ModalBackdropMode.Shared && !ReferenceEquals(_sharedBackdrop, backdrop))
            {
                _sharedBackdrop = backdrop;
                // A context default can be configured while an unshaded modal is open.
                foreach (var entry in _modals) entry.Backdrop = backdrop;
            }
            CompleteChange(enter, null, null, node, enter);
            opened?.Invoke((T)node.Owner, record);
            return (T)node.Owner;
        }, cancellationToken, () =>
        {
            if (Context.IsModalInputBlocked || (Context.InputOwnerNode != null && Context.InputOwnerNode.Parent != this))
                throw new InvalidOperationException("Another modal container owns input or is preparing a modal operation.");
            focus = Context.Host.CaptureFocus();
        });
    }

    public Task CloseAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        CloseInstanceAsync(owner, async (node, ct) =>
        {
            if (!ReferenceEquals(node, CurrentNode)) throw new InvalidOperationException("Only the current modal can be closed.");
            await CloseRangeAsync(_modals.Count - 1, LifecycleReason.Close, playAnimation, ct);
        }, cancellationToken);

    /// <summary>Close and dispose the top modal, restoring the preceding modal if present.</summary>
    public Task CloseCurrentAsync(bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("CloseModal", async ct =>
        {
            if (CurrentNode == null) throw new InvalidOperationException("Cannot close an empty modal container.");
            await CloseRangeAsync(_modals.Count - 1, LifecycleReason.Close, playAnimation, ct);
            return true;
        }, cancellationToken);

    public Task CloseToAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateOpeningAsync("CloseToModal", owner, async (target, ct) =>
        {
            var index = _modals.IndexOf((ModalRecord)target.Presentation!);
            if (index < 0 || target.State != NodeState.Ready)
                throw new InvalidOperationException("Target must be a live modal of this container.");
            if (index == _modals.Count - 1) return true;
            await CloseRangeAsync(index + 1, LifecycleReason.Return, playAnimation, ct);
            return true;
        }, cancellationToken);

    private async Task CloseRangeAsync(int firstRemoved, LifecycleReason reason, bool playAnimation, CancellationToken token)
    {
        var removed = _modals.Skip(firstRemoved).Reverse().ToArray();
        var current = removed[0].Node;
        var target = firstRemoved == 0 ? null : _modals[firstRemoved - 1].Node;
        var focus = removed[removed.Length - 1].Focus;
        var outcome = new TaskCompletionSource<Exception?>();
        foreach (var entry in removed) entry.CloseAttempt = outcome;
        Exception? failure = null;
        try
        {
            var exit = Describe(current, reason, current, target);
            try
            {
                // Reveal the final destination behind the exiting top, not an intermediate modal.
                using var participants = CancellationTokenSource.CreateLinkedTokenSource(
                    removed.Select(entry => entry.Node.Lifetime).Append(target?.Lifetime ?? default).Append(token).ToArray());
                foreach (var entry in removed.Skip(1)) entry.Node.StageVisibility(false);
                UpdateBackdropInput();
                var backdrop = BackdropMode == ModalBackdropMode.PerModal || target == null ? CurrentBackdrop : null;
                await PrepareChangeAsync(exit, null, participants.Token, playAnimation, TransitionDirection.None,
                    backdrop == null ? null : ct => backdrop.HideAsync(playAnimation, ct));
                participants.Token.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                foreach (var entry in removed.Skip(1)) await RecoverPresentationAsync(ex, entry.Node);
                if (CurrentBackdrop is { } backdrop)
                    await Context.CleanupAsync(ex, () => { RestoreBackdrop(backdrop, true); return Task.CompletedTask; });
                await Context.CleanupAsync(ex, () => { UpdateBackdropInput(); return Task.CompletedTask; });
                throw;
            }
            // Commit the final history and input owner once. Intermediate modals never activate.
            _modals.RemoveRange(_modals.Count - removed.Length, removed.Length);
            if (_modals.Count == 0) Context.TrackModal(this, false);
            foreach (var entry in removed) MarkClosed(entry.Node);
            Exception? first = null;
            void Attempt(Action action)
            {
                try { action(); }
                catch (Exception ex)
                { Context.Report(ex, "Modal return completion"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex); }
            }
            foreach (var entry in removed)
            {
                Attempt(() => CommitState(() => ApplyPresentation(entry.Node)));
                if (BackdropMode == ModalBackdropMode.PerModal && entry.Backdrop is { } backdrop)
                    Attempt(() => CommitState(() => { backdrop.SetVisible(false); backdrop.SetInteractive(false); }));
            }
            Attempt(() => CommitState(OrderBackdrops));
            Attempt(() => CompleteChange(exit, current, exit, null, null,
                closingModal: ReferenceEquals(Context.InputOwnerNode, current) ? this : null, restoreFocus: focus));
            foreach (var entry in removed)
            {
                try { await ReleaseClosedAsync(entry.Node, first == null); }
                catch (Exception ex)
                {
                    await Context.CleanupAsync(ex, async () => await entry.Node.DisposeAsync());
                    Context.Report(ex, "Modal return cleanup"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex); }
                try { await ReleaseBackdropAsync(entry); }
                catch (Exception ex)
                {
                    await Context.CleanupAsync(ex, async () => await entry.Node.DisposeAsync());
                    Context.Report(ex, "Modal backdrop cleanup"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex);
                }
            }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            if (failure != null)
                foreach (var record in removed)
                    if (!record.Ended && !(record.Node is IDialogResult dialog && dialog.HasResult))
                        record.CloseAttempt = null; // A restored, unanswered dialog can still complete normally later.
            outcome.SetResult(failure);
        }
    }

    private static async Task AwaitCloseAsync(ModalRecord record)
    {
        var failure = await record.CloseAttempt!.Task;
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private Task CloseDialogOpeningAsync(ModalRecord record, bool playAnimation, CancellationToken token)
    {
        if (record.CloseAttempt != null) return AwaitCloseAsync(record);
        if (record.EndTask != null) return record.EndTask;
        RequireOpening(record.Node, record);
        return CloseAsync(record.Node.Owner, playAnimation, token);
    }

    public async Task<TResult> ShowDialogAsync<T, TResult>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default)
        where T : class, IDialogNode<TResult>
    {
        ModalRecord record = null!;
        Task<TResult> waiting = null!;
        var node = await OpenCoreAsync<T>(args, playAnimation, cancellationToken, (owner, sessionRecord) =>
        { record = sessionRecord; waiting = owner.WaitForResultAsync(cancellationToken); });
        TResult result;
        try
        {
            result = await waiting;
        }
        catch (Exception ex)
        {
            Context.Report(ex, $"Dialog {node.Name}");
            await Context.CleanupAsync(ex, () => CloseDialogOpeningAsync(record, playAnimation, default));
            throw;
        }
        await CloseDialogOpeningAsync(record, playAnimation, cancellationToken);
        return result;
    }

    protected override void OnChildRemoved(UINode child)
    {
        base.OnChildRemoved(child);
        var ownsInput = ReferenceEquals(Context.InputOwnerNode, child);
        if (child.Presentation is not ModalRecord record) return;
        if (_modals.Remove(record) && _modals.Count == 0) Context.TrackModal(this, false);
        if (ownsInput)
        {
            CommitState(() => Context.ReconcileModalInput(this, record.Focus));
            Context.PublishChanges(new LifecycleContext(child, this, LifecycleReason.Disposal, child, CurrentNode), continueOnFailure: true);
        }
    }

    protected override Task OnChildRemovedAsync(UINode child) =>
        child.Presentation is ModalRecord record ? ReleaseBackdropAsync(record) : Task.CompletedTask;

    private async Task ReleaseBackdropAsync(ModalRecord record)
    {
        var backdrop = record.Backdrop;
        if (backdrop == null) return;
        record.Backdrop = null;
        IUIBackdrop? release = null;
        if (BackdropMode == ModalBackdropMode.PerModal || _modals.Count == 0 && ReferenceEquals(_sharedBackdrop, backdrop))
        {
            release = backdrop;
            if (ReferenceEquals(backdrop, _sharedBackdrop)) _sharedBackdrop = null;
        }
        Exception? first = null;
        try { OrderBackdrops(); UpdateBackdropInput(); }
        catch (Exception ex) { BlockOperations(ex); Context.Report(ex, "Restore remaining backdrops"); first = ex; }
        if (release != null)
        {
            if (first != null) await Context.CleanupAsync(first, async () => await release.DisposeAsync());
            else await release.DisposeAsync();
        }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
}

internal interface IDialogResult
{
    bool HasResult { get; }
    void BeginSession();
    void EndSession();
}

public class DialogNode<TResult> : UINode, IDialogResult, IDialogNode<TResult>
{
    public DialogNode() { }
    protected DialogNode(IUINode owner) : base(owner) { }
    private sealed class Session
    {
        internal readonly TaskCompletionSource<TResult> Result = new();
        internal readonly TaskCompletionSource<bool> Closed = new();
        internal bool Completed;
    }
    private Session _session = new();
    bool IDialogResult.HasResult => _session.Completed;
    void IDialogResult.BeginSession() => _session = new Session();
    void IDialogResult.EndSession() { var session = _session; session.Closed.TrySetResult(true); }

    public void Complete(TResult result)
    {
        try
        {
            Context.Host.VerifyAccess(); Context.VerifyMutation();
            if (State != NodeState.Ready || !IsInteractive) throw new InvalidOperationException("Dialog is not active.");
            var session = _session;
            if (session.Completed) throw new InvalidOperationException("Dialog already completed.");
            session.Completed = true;
            Context.Post(() => session.Result.SetResult(result));
        }
        catch (Exception ex) { Context.Report(ex, $"CompleteDialog {Name}"); throw; }
    }

    public async Task<TResult> WaitForResultAsync(CancellationToken token)
    {
        var session = _session;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, Lifetime);
        var canceled = new TaskCompletionSource<bool>();
        using var registration = linked.Token.Register(() => canceled.TrySetResult(true));
        await Task.WhenAny(session.Result.Task, session.Closed.Task, canceled.Task);
        token.ThrowIfCancellationRequested();
        if (session.Completed) return await session.Result.Task;
        linked.Token.ThrowIfCancellationRequested();
        throw new OperationCanceledException("Dialog closed without a result.");
    }
}
