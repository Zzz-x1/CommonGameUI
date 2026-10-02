namespace UIFramework;

public abstract class ContainerNode : UINode
{
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

    protected async Task<T> OperateAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token, Action? beforeLock = null)
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
        }, token);
        return result;
    }
    protected LifecycleContext Describe(UINode node, LifecycleReason reason, UINode? source, UINode? target) =>
        new(node, this, reason, source, target);
    protected void CompleteChange(LifecycleContext operation, UINode? exiting, LifecycleContext? exit,
        UINode? entering, LifecycleContext? enter, Action? focus = null)
    {
        exiting?.SetLocalState(false, false);
        entering?.SetLocalState(true, true);
        if (entering != null) entering.HasEntered = true;
        LockOperation(false);
        focus?.Invoke();
        Context.PublishChanges(operation, exit);
        if (exiting != null) exiting.NotifyExited(exit!);
        if (entering != null) entering.NotifyEntered(enter!);
    }
    protected async Task FinishCloseAsync(UINode node, Action notify)
    {
        try { notify(); }
        catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
        await node.DisposeAsync();
    }

    protected Task PrepareChangeAsync(LifecycleContext? exit, LifecycleContext? enter, CancellationToken token,
        TransitionDirection direction = TransitionDirection.None) => TransitionFlow.PrepareAsync(this, exit, enter, direction, token);

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

    public Task<T> OpenAsync<T>(CancellationToken cancellationToken = default) where T : UINode =>
        OpenCoreAsync<T>(null, false, cancellationToken);

    public Task<T> OpenAsync<T>(object args, CancellationToken cancellationToken = default) where T : UINode =>
        OpenCoreAsync<T>(args, true, cancellationToken);

    private Task<T> OpenCoreAsync<T>(object? args, bool supplied, CancellationToken cancellationToken) where T : UINode =>
        OperateAsync("Open", async ct =>
        {
            ct.ThrowIfCancellationRequested();
            if (supplied && args == null) throw new ArgumentNullException(nameof(args));
            var mode = Context.GetWindowMode<T>();
            if (mode == WindowInstanceMode.Single && _single.TryGetValue(typeof(T), out var existing))
            {
                var record = RequireWindow(existing);
                if (supplied) throw new InvalidOperationException("Creation parameters cannot be supplied to an existing single window; update it through its business API.");
                if (!record.Shown) await ShowCoreAsync(existing, record, ct);
                return (T)existing;
            }
            var node = await Context.CreateAsync<T>(this, args, ct);
            var enter = Describe(node, LifecycleReason.Open, null, node);
            try { await PrepareChangeAsync(null, enter, ct); }
            catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
            _windows.Add(node, new WindowRecord(typeof(T)));
            if (mode == WindowInstanceMode.Single) _single.Add(typeof(T), node);
            CompleteChange(enter, null, null, node, enter);
            return node;
        }, cancellationToken);

    public bool TryGet<T>([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? node) where T : UINode
    {
        try
        {
            Context.Host.VerifyAccess();
            if (State != NodeState.Ready) throw new InvalidOperationException($"Window container is {State}.");
            var matches = _windows.Where(p => p.Value.Type == typeof(T) && p.Key.State == NodeState.Ready).Select(p => p.Key).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException($"Several windows of registered type {typeof(T).Name} exist; use their instance references.");
            node = matches.Length == 0 ? null : (T)matches[0];
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

    public Task HideAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("Hide", async ct =>
        {
            var record = RequireWindow(node);
            if (!record.Shown) throw new InvalidOperationException("Window is already hidden.");
            var exit = Describe(node, LifecycleReason.Hide, node, null);
            try { await PrepareChangeAsync(exit, null, ct); }
            catch (Exception ex) { await Context.CleanupAsync(ex, () => { node.RestoreLocalState(true, true); return Task.CompletedTask; }); throw; }
            record.Shown = false;
            CompleteChange(exit, node, exit, null, null);
            return true;
        }, cancellationToken);

    public Task ShowAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("Show", async ct =>
        {
            var record = RequireWindow(node);
            if (record.Shown) throw new InvalidOperationException("Window is already shown.");
            await ShowCoreAsync(node, record, ct);
            return true;
        }, cancellationToken);

    private async Task ShowCoreAsync(UINode node, WindowRecord record, CancellationToken ct)
    {
        var enter = Describe(node, LifecycleReason.Show, null, node);
        try { await PrepareChangeAsync(null, enter, ct); }
        catch (Exception ex) { await Context.CleanupAsync(ex, () => { node.RestoreLocalState(false, false); return Task.CompletedTask; }); throw; }
        record.Shown = true;
        CompleteChange(enter, null, null, node, enter);
    }

    public Task CloseAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("Close", async ct =>
        {
            var record = RequireWindow(node);
            if (!record.Shown)
            {
                ct.ThrowIfCancellationRequested();
                RemoveWindow(node);
                await node.DisposeAsync();
                return true;
            }
            var exit = Describe(node, LifecycleReason.Close, node, null);
            try { await PrepareChangeAsync(exit, null, ct); }
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
    protected override void OnChildRemoved(UINode child) => RemoveWindow(child);
}

public class StackContainerNode : ContainerNode
{
    private readonly List<UINode> _history = new();
    public IReadOnlyList<UINode> History => _history.AsReadOnly();
    public UINode? Current => _history.Count == 0 ? null : _history[_history.Count - 1];

    public Task<T> PushAsync<T>(object? args = null, CancellationToken cancellationToken = default) where T : UINode =>
        OperateAsync("Push", async ct =>
        {
            var previous = Current;
            var node = await Context.CreateAsync<T>(this, args, ct);
            var enter = Describe(node, LifecycleReason.Push, previous, node);
            var exit = previous == null ? null : Describe(previous, LifecycleReason.Covered, previous, node);
            try
            {
                await PrepareChangeAsync(exit, enter, ct, TransitionDirection.Forward);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                if (previous != null) await Context.CleanupAsync(ex, () => { previous.RestoreLocalState(true, true); return Task.CompletedTask; });
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            _history.Add(node);
            CompleteChange(enter, previous, exit, node, enter);
            return node;
        }, cancellationToken);

    public Task PopAsync(CancellationToken cancellationToken = default) => OperateAsync("Pop", async ct =>
    {
        if (Current == null) throw new InvalidOperationException("Cannot pop an empty stack.");
        await ReturnCoreAsync(_history.Count - 2, ct);
        return true;
    }, cancellationToken);

    public Task PopToAsync(UINode target, CancellationToken cancellationToken = default) => OperateAsync("PopTo", async ct =>
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        var index = _history.IndexOf(target);
        if (target.Parent != this || target.State != NodeState.Ready || index < 0 || index == _history.Count - 1)
            throw new InvalidOperationException("Target must be an earlier available instance in this stack's history.");
        await ReturnCoreAsync(index, ct);
        return true;
    }, cancellationToken);

    private async Task ReturnCoreAsync(int targetIndex, CancellationToken ct)
    {
        var snapshot = _history.ToArray();
        var current = snapshot[snapshot.Length - 1];
        var previous = targetIndex < 0 ? null : snapshot[targetIndex];
        var removed = snapshot.Skip(targetIndex + 1).Reverse().ToArray();
        var exit = Describe(current, LifecycleReason.Close, current, previous);
        var enter = previous == null ? null : Describe(previous, LifecycleReason.Return, current, previous);
        try
        {
            await PrepareChangeAsync(exit, enter, ct, TransitionDirection.Backward);
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
            try { await node.DisposeAsync(); }
            catch (Exception ex)
            {
                Context.Report(ex, "Return cleanup");
                if (first == null) first = ex; else UIFailure.AddSecondary(first, ex);
            }
        }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }

    protected override void OnChildRemoved(UINode child) => _history.Remove(child);
}

/// <summary>Switcher for existing embedded nodes. Names are ordinal and container-local.</summary>
public class TabContainerNode : ContainerNode
{
    private readonly Dictionary<string, UINode> _items = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, UINode> Items => new System.Collections.ObjectModel.ReadOnlyDictionary<string, UINode>(_items);
    public string? CurrentName { get; private set; }
    public UINode? Current => CurrentName == null ? null : _items[CurrentName];

    public Task BindAsync(string name, UINode node, IUIElement? embeddedElement = null,
        CancellationToken cancellationToken = default) => OperateAsync("BindTab", async ct =>
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tab name is required.", nameof(name));
        if (_items.ContainsKey(name)) throw new InvalidOperationException($"Duplicate tab name '{name}'.");
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (node.State != NodeState.Detached) throw new InvalidOperationException("Tab node is already attached.");
        await node.AttachAsync(this, name, embeddedElement, ct);
        _items.Add(name, node);
        return true;
    }, cancellationToken);

    public Task SwitchAsync(string name, CancellationToken cancellationToken = default) =>
        OperateAsync("SwitchTab", async ct =>
        {
            if (!_items.TryGetValue(name, out var next)) throw new KeyNotFoundException($"Tab '{name}' does not exist in '{Name}'.");
            if (ReferenceEquals(Current, next)) throw new InvalidOperationException($"Tab '{name}' is already current.");
            var previous = Current;
            var enter = Describe(next, LifecycleReason.Switch, previous, next);
            var exit = previous == null ? null : Describe(previous, LifecycleReason.Switch, previous, next);
            try
            {
                await PrepareChangeAsync(exit, enter, ct);
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

    public async Task SwitchAsync<T>(CancellationToken cancellationToken = default) where T : UINode
    {
        try
        {
            var names = _items.Where(pair => pair.Value is T).Select(pair => pair.Key).ToArray();
            if (names.Length != 1) throw new InvalidOperationException($"Expected one tab of type {typeof(T).Name}, found {names.Length}; use its name.");
            await SwitchAsync(names[0], cancellationToken);
        }
        catch (Exception ex) { Context.Report(ex, $"SwitchTab<{typeof(T).Name}> {Name}"); throw; }
    }

    protected override void OnChildRemoved(UINode child)
    {
        var name = _items.FirstOrDefault(pair => ReferenceEquals(pair.Value, child)).Key;
        if (name != null) { _items.Remove(name); if (CurrentName == name) CurrentName = null; }
    }
}

/// <summary>Topmost modal owns input; this container owns its configured backdrop displays.</summary>
public class ModalContainerNode : ContainerNode
{
    private readonly List<UINode> _modals = new();
    private readonly Dictionary<UINode, object?> _focus = new();
    private readonly Dictionary<UINode, IUIBackdrop> _backdrops = new();
    private IUIBackdropFactory? _backdropFactory;
    private IUIBackdrop? _sharedBackdrop;
    private bool _backdropLocked;
    private bool _dismissPending;
    private bool _ownsInputBlock;
    public ModalBackdropMode BackdropMode { get; private set; } = ModalBackdropMode.Shared;
    public IUIBackdrop? CurrentBackdrop => Current != null && _backdrops.TryGetValue(Current, out var backdrop) ? backdrop : null;
    public UINode? Current => _modals.LastOrDefault();

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
            var node = Current ?? throw new InvalidOperationException("No modal is available to dismiss.");
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
            backdrop.SetVisible(IsVisible && _modals.Any(n => _backdrops.TryGetValue(n, out var owned) && ReferenceEquals(owned, backdrop)));
            backdrop.SetInteractive(!_backdropLocked && IsInteractive && ReferenceEquals(backdrop, CurrentBackdrop));
        }
    }
    protected override void OnApplyState() => UpdateBackdropInput();
    private void OrderBackdrops()
    {
        if (BackdropMode == ModalBackdropMode.Shared)
        {
            foreach (var modal in _modals.Where(n => !ReferenceEquals(n, Current))) modal.Element?.BringToFront();
            _sharedBackdrop?.BringToFront(); Current?.Element?.BringToFront();
        }
        else foreach (var modal in _modals)
        {
            if (_backdrops.TryGetValue(modal, out var backdrop)) backdrop.BringToFront();
            modal.Element?.BringToFront();
        }
    }

    public Task<T> OpenAsync<T>(object? args = null, CancellationToken cancellationToken = default) where T : UINode
    {
        object? focus = null;
        return OperateAsync("OpenModal", async ct =>
        {
            if (Context.InputOwner != null && Context.InputOwner.Parent != this)
                throw new InvalidOperationException("Independent modal containers cannot own input simultaneously.");
            var previous = Current;
            var node = await Context.CreateAsync<T>(this, args, ct);
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
                        await backdrop.ShowAsync(ct);
                    }
                    else priorOrder = backdrop.CaptureOrder();
                    backdrop.BringToFront();
                    node.Element?.BringToFront();
                }
                await PrepareChangeAsync(null, enter, ct);
                node.Element?.BringToFront();
                ct.ThrowIfCancellationRequested();
                if (Context.InputOwner != null && Context.InputOwner.Parent != this)
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
            _modals.Add(node); _focus.Add(node, focus);
            if (backdrop != null)
            {
                _backdrops.Add(node, backdrop);
                if (BackdropMode == ModalBackdropMode.Shared) _sharedBackdrop = backdrop;
            }
            Context.SetInputOwner(node);
            CompleteChange(enter, null, null, node, enter, () => Context.Host.Focus(node.Element));
            return node;
        }, cancellationToken, () =>
        {
            if (Context.IsModalInputBlocked || (Context.InputOwner != null && Context.InputOwner.Parent != this))
                throw new InvalidOperationException("Another modal container owns input or is preparing a modal operation.");
            focus = Context.Host.CaptureFocus();
        });
    }

    public Task CloseAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("CloseModal", async ct =>
        {
            if (!ReferenceEquals(node, Current)) throw new InvalidOperationException("Only the current modal can be closed.");
            var focus = _focus[node];
            var exit = Describe(node, LifecycleReason.Close, node, _modals.Count > 1 ? _modals[_modals.Count - 2] : null);
            try
            {
                await PrepareChangeAsync(exit, null, ct);
                if (CurrentBackdrop is { } backdrop && (BackdropMode == ModalBackdropMode.PerModal || _modals.Count == 1))
                    await backdrop.HideAsync(ct);
            }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, () => { node.RestoreLocalState(true, true); return Task.CompletedTask; });
                if (CurrentBackdrop is { } backdrop)
                    await Context.CleanupAsync(ex, () => { backdrop.Restore(true); return Task.CompletedTask; });
                throw;
            }
            _modals.Remove(node); _focus.Remove(node);
            await FinishCloseAsync(node, () =>
            {
                Context.SetInputOwner(Current);
                OrderBackdrops();
                CompleteChange(exit, node, exit, null, null, () => Context.Host.RestoreFocus(focus));
            });
            return true;
        }, cancellationToken);

    public async Task<TResult> ShowDialogAsync<T, TResult>(object? args = null, CancellationToken cancellationToken = default)
        where T : DialogNode<TResult>
    {
        var node = await OpenAsync<T>(args, cancellationToken);
        try
        {
            var result = await node.WaitForResultAsync(cancellationToken);
            await CloseAsync(node, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            Context.Report(ex, $"Dialog {node.Name}");
            if (node.State == NodeState.Ready)
                await Context.CleanupAsync(ex, () => CloseAsync(node));
            throw;
        }
    }

    protected override void OnChildRemoved(UINode child)
    {
        var ownsInput = ReferenceEquals(Context.InputOwner, child);
        _focus.TryGetValue(child, out var focus);
        _modals.Remove(child); _focus.Remove(child);
        if (ownsInput)
        {
            Context.SetInputOwner(Current);
            Context.Host.RestoreFocus(focus);
            Context.PublishChanges(new LifecycleContext(child, this, LifecycleReason.Disposal, child, Current), continueOnFailure: true);
        }
    }

    protected override async Task OnChildRemovedAsync(UINode child)
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

public class DialogNode<TResult> : UINode
{
    private readonly TaskCompletionSource<TResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete(TResult result)
    {
        try
        {
            Context.Host.VerifyAccess();
            if (State != NodeState.Ready || !IsInteractive) throw new InvalidOperationException("Dialog is not active.");
            if (!_result.TrySetResult(result)) throw new InvalidOperationException("Dialog already completed.");
        }
        catch (Exception ex) { Context.Report(ex, $"CompleteDialog {Name}"); throw; }
    }

    internal async Task<TResult> WaitForResultAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, Lifetime);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = linked.Token.Register(() => canceled.TrySetResult(true));
        await Task.WhenAny(_result.Task, canceled.Task);
        linked.Token.ThrowIfCancellationRequested();
        return await _result.Task;
    }
}
