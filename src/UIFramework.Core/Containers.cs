namespace UIFramework;

public abstract class ContainerNode : UINode
{
    private readonly Dictionary<Type, UINode> _cache = new();

    protected async Task<UINode> AcquireAsync<T>(object? args, CancellationToken token) where T : class, IUINode
    {
        token.ThrowIfCancellationRequested();
        UINode node;
        if (_cache.TryGetValue(typeof(T), out node!))
        {
            _cache.Remove(typeof(T)); node.Cached = false; node.CacheReuse = true;
            try
            {
                if (node.SuspendedInputOwner != null && Context.InputOwnerNode != null)
                    throw new InvalidOperationException("Another modal currently owns input; cannot reopen this cached subtree.");
                node.SetParameters(args);
                if (node is IDialogResult dialog) dialog.BeginSession();
            }
            catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
        }
        else node = await Context.CreateAsync<T>(this, args, token);
        return node;
    }

    private bool SuspendOwnedInput(UINode node)
    {
        if (Context.InputOwnerNode is not { } inputOwner) return false;
        for (var parent = inputOwner; parent != null; parent = parent.Parent)
            if (ReferenceEquals(parent, node))
            { node.SuspendedInputOwner = inputOwner; Context.SetInputOwner(null); return true; }
        return false;
    }

    protected async Task ReleaseClosedAsync(UINode node, bool retain = true)
    {
        if (node is IDialogResult dialog) dialog.EndSession();
        node.CacheReuse = false;
        if (!retain || node.CacheMode == NodeCacheMode.None || State != NodeState.Ready)
        { await node.DisposeAsync(); return; }
        try
        {
            if (SuspendOwnedInput(node)) Context.PublishChanges(new LifecycleContext(node, this, LifecycleReason.Close));
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
        set { try { if (State != NodeState.Detached) { Context.Host.VerifyAccess(); Context.VerifyMutation(); } _childTransition = value; }
            catch (Exception ex) { ReportFailure(ex, "Configure child transition"); throw; } }
    }
    protected virtual void OnOperationLockChanged(bool locked) { }
    private void LockOperation(bool locked) { SetInputLocked(locked); OnOperationLockChanged(locked); }
    protected override bool InitiallyVisible => true;

    protected async Task<T> OperateAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token, Action? beforeLock = null, bool allowCached = false)
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
        UINode? entering, LifecycleContext? enter, Action? focus = null)
    {
        if (exiting != null && exit?.Reason == LifecycleReason.Close) SuspendOwnedInput(exiting);
        if (entering?.SuspendedInputOwner is { State: NodeState.Ready } retainedInput)
        { Context.SetInputOwner(retainedInput); entering.SuspendedInputOwner = null; }
        exiting?.SetLocalState(false, false);
        entering?.SetLocalState(true, true);
        if (entering != null) entering.HasEntered = true;
        LockOperation(false);
        focus?.Invoke();
        Context.PublishChanges(operation, exit);
        if (exiting != null) exiting.NotifyExited(exit!);
        if (entering != null) { entering.NotifyEntered(enter!); entering.CacheReuse = false; }
    }
    protected async Task FinishCloseAsync(UINode node, Action notify)
    {
        try { notify(); }
        catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
        await ReleaseClosedAsync(node);
    }

    protected Task PrepareChangeAsync(LifecycleContext? exit, LifecycleContext? enter, CancellationToken token, bool playAnimation,
        TransitionDirection direction = TransitionDirection.None) => TransitionFlow.PrepareAsync(this, exit, enter, direction, playAnimation, token);

}

/// <summary>Independent windows with registration-defined instance identity and retained hiding.</summary>
public class PanelContainerNode : ContainerNode
{
    private sealed class WindowRecord
    {
        public WindowRecord(Type type) { Type = type; }
        public Type Type { get; }
        public bool Shown { get; set; } = true;
    }
    private readonly Dictionary<UINode, WindowRecord> _windows = new();
    private readonly Dictionary<Type, UINode> _single = new();

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
            _windows.Add(node, new WindowRecord(typeof(T)));
            if (mode == WindowInstanceMode.Single) _single.Add(typeof(T), node);
            try { CompleteChange(enter, null, null, node, enter); }
            catch (Exception ex)
            {
                if (node.CacheReuse) await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            return (T)node.Owner;
        }, cancellationToken);

    public bool TryGet<T>([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? node) where T : class, IUINode
    {
        try
        {
            Context.Host.VerifyAccess();
            if (State != NodeState.Ready) throw new InvalidOperationException($"Window container is {State}.");
            var matches = _windows.Where(p => p.Value.Type == typeof(T) && p.Key.State == NodeState.Ready).Select(p => p.Key).ToArray();
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
        OperateAsync("Hide", async ct =>
        {
            var node = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
            var record = RequireWindow(node);
            if (!record.Shown) throw new InvalidOperationException("Window is already hidden.");
            var exit = Describe(node, LifecycleReason.Hide, node, null);
            try { await PrepareChangeAsync(exit, null, ct, playAnimation); }
            catch (Exception ex) { await Context.CleanupAsync(ex, () => { node.RestoreLocalState(true, true); return Task.CompletedTask; }); throw; }
            record.Shown = false;
            CompleteChange(exit, node, exit, null, null);
            return true;
        }, cancellationToken);

    public Task ShowAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("Show", async ct =>
        {
            var node = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
            var record = RequireWindow(node);
            if (record.Shown) throw new InvalidOperationException("Window is already shown.");
            await ShowCoreAsync(node, record, playAnimation, ct);
            return true;
        }, cancellationToken);

    private async Task ShowCoreAsync(UINode node, WindowRecord record, bool playAnimation, CancellationToken ct)
    {
        var enter = Describe(node, LifecycleReason.Show, null, node);
        try { await PrepareChangeAsync(null, enter, ct, playAnimation); }
        catch (Exception ex) { await Context.CleanupAsync(ex, () => { node.RestoreLocalState(false, false); return Task.CompletedTask; }); throw; }
        record.Shown = true;
        CompleteChange(enter, null, null, node, enter);
    }

    public Task CloseAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("Close", async ct =>
        {
            var node = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
            var record = RequireWindow(node);
            if (!record.Shown)
            {
                ct.ThrowIfCancellationRequested();
                RemoveWindow(node);
                await ReleaseClosedAsync(node);
                return true;
            }
            var exit = Describe(node, LifecycleReason.Close, node, null);
            try { await PrepareChangeAsync(exit, null, ct, playAnimation); }
            catch (Exception ex) { await Context.CleanupAsync(ex, () => { node.RestoreLocalState(true, true); return Task.CompletedTask; }); throw; }
            RemoveWindow(node);
            await FinishCloseAsync(node, () => CompleteChange(exit, node, exit, null, null));
            return true;
        }, cancellationToken);

    private void RemoveWindow(UINode node)
    {
        if (_windows.TryGetValue(node, out var record))
        {
            _windows.Remove(node);
            if (_single.TryGetValue(record.Type, out var single) && ReferenceEquals(single, node)) _single.Remove(record.Type);
        }
    }
    protected override void OnChildRemoved(UINode child) { RemoveWindow(child); base.OnChildRemoved(child); }
}

public class StackContainerNode : ContainerNode
{
    private readonly List<UINode> _history = new();
    public IReadOnlyList<IUINode> History => _history.Select(n => n.Owner).ToArray();
    public IUINode? Current => CurrentNode?.Owner;
    private UINode? CurrentNode => _history.Count == 0 ? null : _history[_history.Count - 1];

    public Task<T> PushAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode =>
        OperateAsync("Push", async ct =>
        {
            var previous = CurrentNode;
            var node = await AcquireAsync<T>(args, ct);
            var enter = Describe(node, LifecycleReason.Push, previous, node);
            var exit = previous == null ? null : Describe(previous, LifecycleReason.Covered, previous, node);
            try
            {
                await PrepareChangeAsync(exit, enter, ct, playAnimation, TransitionDirection.Forward);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                if (previous != null) await Context.CleanupAsync(ex, () => { previous.RestoreLocalState(true, true); return Task.CompletedTask; });
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            _history.Add(node);
            try { CompleteChange(enter, previous, exit, node, enter); }
            catch (Exception ex)
            {
                if (node.CacheReuse)
                {
                    await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                    if (previous != null) await Context.CleanupAsync(ex, () =>
                    { previous.SetLocalState(true, true); Context.PublishChanges(exit!); return Task.CompletedTask; });
                }
                throw;
            }
            return (T)node.Owner;
        }, cancellationToken);

    public Task PopAsync(bool playAnimation = true, CancellationToken cancellationToken = default) => OperateAsync("Pop", async ct =>
    {
        if (CurrentNode == null) throw new InvalidOperationException("Cannot pop an empty stack.");
        await ReturnCoreAsync(_history.Count - 2, playAnimation, ct);
        return true;
    }, cancellationToken);

    public Task PopToAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) => OperateAsync("PopTo", async ct =>
    {
        var target = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
        if (target == null) throw new ArgumentNullException(nameof(target));
        var index = _history.IndexOf(target);
        if (target.Parent != this || target.State != NodeState.Ready || index < 0 || index == _history.Count - 1)
            throw new InvalidOperationException("Target must be an earlier available instance in this stack's history.");
        await ReturnCoreAsync(index, playAnimation, ct);
        return true;
    }, cancellationToken);

    private async Task ReturnCoreAsync(int targetIndex, bool playAnimation, CancellationToken ct)
    {
        var snapshot = _history.ToArray();
        var current = snapshot[snapshot.Length - 1];
        var previous = targetIndex < 0 ? null : snapshot[targetIndex];
        var removed = snapshot.Skip(targetIndex + 1).Reverse().ToArray();
        var exit = Describe(current, LifecycleReason.Close, current, previous);
        var enter = previous == null ? null : Describe(previous, LifecycleReason.Return, current, previous);
        try
        {
            await PrepareChangeAsync(exit, enter, ct, playAnimation, TransitionDirection.Backward);
            ct.ThrowIfCancellationRequested();
            if (!_history.SequenceEqual(snapshot)) throw new InvalidOperationException("History changed while preparing the return.");
        }
        catch (Exception ex)
        {
            if (previous != null) await Context.CleanupAsync(ex, () => { previous.RestoreLocalState(false, false); return Task.CompletedTask; });
            await Context.CleanupAsync(ex, () => { current.RestoreLocalState(true, true); return Task.CompletedTask; });
            throw;
        }
        _history.RemoveRange(targetIndex + 1, _history.Count - targetIndex - 1);
        Exception? first = null;
        try { CompleteChange(exit, current, exit, previous, enter); }
        catch (Exception ex) { Context.Report(ex, "Return completion"); first = ex; }
        foreach (var node in removed)
        {
            try { await ReleaseClosedAsync(node, first == null); }
            catch (Exception ex)
            {
                Context.Report(ex, "Return cleanup");
                if (first == null) first = ex; else UIFailure.AddSecondary(first, ex);
            }
        }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }

    protected override void OnChildRemoved(UINode child) { _history.Remove(child); base.OnChildRemoved(child); }
}

/// <summary>Switcher for existing embedded nodes. Names are ordinal and container-local.</summary>
public class TabContainerNode : ContainerNode
{
    private readonly Dictionary<string, UINode> _items = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, IUINode> Items => _items.ToDictionary(p => p.Key, p => p.Value.Owner, StringComparer.Ordinal);
    public string? CurrentName { get; private set; }
    public IUINode? Current => CurrentNode?.Owner;
    private UINode? CurrentNode => CurrentName == null ? null : _items[CurrentName];

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
        OperateAsync("SwitchTab", async ct =>
        {
            if (!_items.TryGetValue(name, out var next)) throw new KeyNotFoundException($"Tab '{name}' does not exist in '{Name}'.");
            if (ReferenceEquals(CurrentNode, next)) throw new InvalidOperationException($"Tab '{name}' is already current.");
            var previous = CurrentNode;
            var enter = Describe(next, LifecycleReason.Switch, previous, next);
            var exit = previous == null ? null : Describe(previous, LifecycleReason.Switch, previous, next);
            try
            {
                await PrepareChangeAsync(exit, enter, ct, playAnimation);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, () => { next.RestoreLocalState(false, false); return Task.CompletedTask; });
                if (previous != null) await Context.CleanupAsync(ex, () => { previous.RestoreLocalState(true, true); return Task.CompletedTask; });
                throw;
            }
            CurrentName = name;
            CompleteChange(enter, previous, exit, next, enter);
            return true;
        }, cancellationToken);

    public async Task SwitchAsync<T>(bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode
    {
        try
        {
            var names = _items.Where(pair => pair.Value.Owner is T).Select(pair => pair.Key).ToArray();
            if (names.Length != 1) throw new InvalidOperationException($"Expected one tab of type {typeof(T).Name}, found {names.Length}; use its name.");
            await SwitchAsync(names[0], playAnimation, cancellationToken);
        }
        catch (Exception ex) { Context.Report(ex, $"SwitchTab<{typeof(T).Name}> {Name}"); throw; }
    }

    public Task HideAsync(bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("HideTab", async ct =>
        {
            var current = CurrentNode ?? throw new InvalidOperationException("No tab is selected.");
            var exit = Describe(current, LifecycleReason.Hide, current, null);
            try { await PrepareChangeAsync(exit, null, ct, playAnimation); ct.ThrowIfCancellationRequested(); }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, () => { current.RestoreLocalState(true, true); return Task.CompletedTask; });
                throw;
            }
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
    private sealed class ModalRecord(object? focus)
    {
        public object? Focus { get; } = focus;
        // The outcome is shared with result waiters, including failed close attempts.
        public TaskCompletionSource<Exception?>? CloseAttempt;
    }
    private readonly List<UINode> _modals = new();
    private readonly Dictionary<UINode, ModalRecord> _records = new();
    private readonly Dictionary<UINode, IUIBackdrop> _backdrops = new();
    private IUIBackdropFactory? _backdropFactory;
    private IUIBackdrop? _sharedBackdrop;
    private bool _backdropLocked;
    private bool _dismissPending;
    private bool _ownsInputBlock;
    public ModalBackdropMode BackdropMode { get; private set; } = ModalBackdropMode.Shared;
    public IUIBackdrop? CurrentBackdrop => CurrentNode != null && _backdrops.TryGetValue(CurrentNode, out var backdrop) ? backdrop : null;
    public IUINode? Current => CurrentNode?.Owner;
    private UINode? CurrentNode => _modals.LastOrDefault();

    public void ConfigureBackdrop(IUIBackdropFactory factory, ModalBackdropMode mode = ModalBackdropMode.Shared)
    {
        try
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!Enum.IsDefined(typeof(ModalBackdropMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (State != NodeState.Detached) { Context.Host.VerifyAccess(); Context.VerifyMutation(); }
            if (_modals.Count != 0 || _backdropLocked || State is NodeState.Disposing or NodeState.Disposed)
                throw new InvalidOperationException("Configure backdrops before opening modals.");
            _backdropFactory = factory; BackdropMode = mode;
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
        foreach (var backdrop in _backdrops.Values.Distinct())
        {
            backdrop.SetVisible(IsVisible && _modals.Any(n => n.IsVisible && _backdrops.TryGetValue(n, out var owned) && ReferenceEquals(owned, backdrop)));
            backdrop.SetInteractive(!_backdropLocked && IsInteractive && ReferenceEquals(backdrop, CurrentBackdrop));
        }
    }
    protected override void OnApplyState() => UpdateBackdropInput();
    private void OrderBackdrops()
    {
        if (BackdropMode == ModalBackdropMode.Shared)
        {
            foreach (var modal in _modals.Where(n => !ReferenceEquals(n, CurrentNode))) modal.Element?.BringToFront();
            _sharedBackdrop?.BringToFront(); CurrentNode?.Element?.BringToFront();
        }
        else foreach (var modal in _modals)
        {
            if (_backdrops.TryGetValue(modal, out var backdrop)) backdrop.BringToFront();
            modal.Element?.BringToFront();
        }
    }

    public Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode =>
        OpenCoreAsync<T>(args, playAnimation, cancellationToken);
    private Task<T> OpenCoreAsync<T>(object? args, bool playAnimation, CancellationToken cancellationToken, Action<T, ModalRecord>? opened = null) where T : class, IUINode
    {
        object? focus = null;
        return OperateAsync("OpenModal", async ct =>
        {
            if (Context.InputOwnerNode != null && Context.InputOwnerNode.Parent != this)
                throw new InvalidOperationException("Independent modal containers cannot own input simultaneously.");
            var previous = CurrentNode;
            var node = await AcquireAsync<T>(args, ct);
            var enter = Describe(node, LifecycleReason.Open, previous, node);
            IUIBackdrop? backdrop = null;
            var createdBackdrop = false;
            object? priorOrder = null;
            try
            {
                if (_backdropFactory != null)
                {
                    backdrop = BackdropMode == ModalBackdropMode.Shared ? _sharedBackdrop : null;
                    if (backdrop == null)
                    {
                        backdrop = await _backdropFactory.CreateAsync(Element, () => RequestDismissAsync(), ct)
                            ?? throw new InvalidOperationException("Backdrop factory returned null.");
                        createdBackdrop = true;
                        backdrop.SetInteractive(false);
                    }
                    else priorOrder = backdrop.CaptureOrder();
                    backdrop.BringToFront();
                    node.Element?.BringToFront();
                }
                await TransitionFlow.PrepareAsync(this, null, enter, TransitionDirection.None, playAnimation, ct,
                    createdBackdrop ? token => backdrop!.ShowAsync(playAnimation, token) : null);
                node.Element?.BringToFront();
                ct.ThrowIfCancellationRequested();
                if (Context.InputOwnerNode != null && Context.InputOwnerNode.Parent != this)
                    throw new InvalidOperationException("Another modal container acquired input while this modal was preparing.");
            }
            catch (Exception ex)
            {
                if (backdrop != null)
                {
                    if (createdBackdrop) await Context.CleanupAsync(ex, async () => await backdrop.DisposeAsync());
                    else if (priorOrder != null) await Context.CleanupAsync(ex, () => { backdrop.RestoreOrder(priorOrder); return Task.CompletedTask; });
                }
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            _modals.Add(node); _records.Add(node, new ModalRecord(focus));
            if (backdrop != null)
            {
                _backdrops.Add(node, backdrop);
                if (BackdropMode == ModalBackdropMode.Shared) _sharedBackdrop = backdrop;
            }
            Context.SetInputOwner(node);
            try { CompleteChange(enter, null, null, node, enter, () => Context.Host.Focus(node.Element)); }
            catch (Exception ex)
            {
                if (node.CacheReuse) await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            opened?.Invoke((T)node.Owner, _records[node]);
            return (T)node.Owner;
        }, cancellationToken, () =>
        {
            if (Context.IsModalInputBlocked || (Context.InputOwnerNode != null && Context.InputOwnerNode.Parent != this))
                throw new InvalidOperationException("Another modal container owns input or is preparing a modal operation.");
            focus = Context.Host.CaptureFocus();
        });
    }

    public Task CloseAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("CloseModal", async ct =>
        {
            var node = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!ReferenceEquals(node, CurrentNode)) throw new InvalidOperationException("Only the current modal can be closed.");
            await CloseRangeAsync(_modals.Count - 1, LifecycleReason.Close, playAnimation, ct);
            return true;
        }, cancellationToken);

    public Task CloseToAsync(IUINode owner, bool playAnimation = true, CancellationToken cancellationToken = default) =>
        OperateAsync("CloseToModal", async ct =>
        {
            var target = owner?.Node ?? throw new ArgumentNullException(nameof(owner));
            if (target == null) throw new ArgumentNullException(nameof(target));
            var index = _modals.IndexOf(target);
            if (index < 0 || target.State != NodeState.Ready || index == _modals.Count - 1)
                throw new InvalidOperationException("Target must be a live lower modal of this container.");
            await CloseRangeAsync(index + 1, LifecycleReason.Return, playAnimation, ct);
            return true;
        }, cancellationToken);

    private async Task CloseRangeAsync(int firstRemoved, LifecycleReason reason, bool playAnimation, CancellationToken token)
    {
        var removed = _modals.Skip(firstRemoved).Reverse().ToArray();
        var current = removed[0];
        var target = firstRemoved == 0 ? null : _modals[firstRemoved - 1];
        var focus = _records[removed[removed.Length - 1]].Focus;
        var outcome = new TaskCompletionSource<Exception?>();
        foreach (var node in removed) _records[node].CloseAttempt = outcome;
        Exception? failure = null;
        try
        {
            var exit = Describe(current, reason, current, target);
            try
            {
                // Reveal the final destination behind the exiting top, not an intermediate modal.
                foreach (var node in removed.Skip(1)) node.StageVisibility(false);
                UpdateBackdropInput();
                var backdrop = BackdropMode == ModalBackdropMode.PerModal || target == null ? CurrentBackdrop : null;
                await TransitionFlow.PrepareAsync(this, exit, null, TransitionDirection.None, playAnimation, token,
                    backdrop == null ? null : ct => backdrop.HideAsync(playAnimation, ct));
                token.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                foreach (var node in removed)
                    await Context.CleanupAsync(ex, () => { node.RestoreLocalState(true, true); return Task.CompletedTask; });
                if (CurrentBackdrop is { } backdrop)
                    await Context.CleanupAsync(ex, () => { backdrop.Restore(true); return Task.CompletedTask; });
                await Context.CleanupAsync(ex, () => { UpdateBackdropInput(); return Task.CompletedTask; });
                throw;
            }
            // Commit the final history and input owner once. Intermediate modals never activate.
            _modals.RemoveRange(firstRemoved, _modals.Count - firstRemoved);
            foreach (var node in removed) _records.Remove(node);
            Exception? first = null;
            void Attempt(Action action)
            {
                try { action(); }
                catch (Exception ex)
                { Context.Report(ex, "Modal return completion"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex); }
            }
            foreach (var node in removed) Attempt(() => node.SetLocalState(false, false));
            Attempt(() => Context.SetInputOwner(target));
            Attempt(OrderBackdrops);
            Attempt(() => CompleteChange(exit, current, exit, null, null, () => Context.Host.RestoreFocus(focus)));
            foreach (var node in removed)
            {
                try
                {
                    await ReleaseClosedAsync(node, first == null);
                    await ReleaseBackdropAsync(node);
                }
                catch (Exception ex)
                {
                    await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                    Context.Report(ex, "Modal return cleanup"); if (first == null) first = ex; else UIFailure.AddSecondary(first, ex); }
            }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            if (failure != null)
                foreach (var node in removed)
                    if (_records.TryGetValue(node, out var record) && !(node is IDialogResult dialog && dialog.HasResult))
                        record.CloseAttempt = null; // A restored, unanswered dialog can still complete normally later.
            outcome.SetResult(failure);
        }
    }

    private static async Task AwaitCloseAsync(ModalRecord record)
    {
        var failure = await record.CloseAttempt!.Task;
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
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
            await Context.CleanupAsync(ex, () => record.CloseAttempt != null ? AwaitCloseAsync(record) :
                node.State == NodeState.Ready ? CloseAsync(node, playAnimation) : node.DisposeAsync().AsTask());
            throw;
        }
        if (record.CloseAttempt != null) await AwaitCloseAsync(record);
        else if (node.State == NodeState.Ready) await CloseAsync(node, playAnimation, cancellationToken);
        else await node.DisposeAsync();
        return result;
    }

    protected override void OnChildRemoved(UINode child)
    {
        base.OnChildRemoved(child);
        var ownsInput = ReferenceEquals(Context.InputOwnerNode, child);
        _records.TryGetValue(child, out var record);
        _modals.Remove(child); _records.Remove(child);
        if (ownsInput)
        {
            Context.SetInputOwner(CurrentNode);
            Context.Host.RestoreFocus(record?.Focus);
            Context.PublishChanges(new LifecycleContext(child, this, LifecycleReason.Disposal, child, CurrentNode), continueOnFailure: true);
        }
    }

    protected override Task OnChildRemovedAsync(UINode child) => ReleaseBackdropAsync(child);

    private async Task ReleaseBackdropAsync(UINode child)
    {
        IUIBackdrop? release = null;
        if (_backdrops.Remove(child, out var backdrop) && (BackdropMode == ModalBackdropMode.PerModal || _modals.Count == 0))
        {
            release = backdrop;
            if (ReferenceEquals(backdrop, _sharedBackdrop)) _sharedBackdrop = null;
        }
        Exception? first = null;
        try { OrderBackdrops(); UpdateBackdropInput(); }
        catch (Exception ex) { Context.Report(ex, "Restore remaining backdrops"); first = ex; }
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
