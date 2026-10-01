namespace UIFramework;

public abstract class ContainerNode : UINode
{
    protected override bool InitiallyVisible => true;

    protected async Task<T> OperateAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token, Action? beforeLock = null)
    {
        T result = default!;
        await RunAsync(operation, async ct =>
        {
            Exception? primary = null;
            try { beforeLock?.Invoke(); SetInputLocked(true); result = await action(ct); }
            catch (Exception ex) { primary = ex; throw; }
            finally
            {
                if (State == NodeState.Ready)
                {
                    if (primary == null) SetInputLocked(false);
                    else await Context.CleanupAsync(primary, () => { SetInputLocked(false); return Task.CompletedTask; });
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
        SetInputLocked(false);
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

}

/// <summary>Independent instances; no implicit history or single-instance lookup.</summary>
public class PanelContainerNode : ContainerNode
{
    public Task<T> OpenAsync<T>(object? args = null, CancellationToken cancellationToken = default) where T : UINode =>
        OperateAsync("Open", async ct =>
        {
            var node = await Context.CreateAsync<T>(this, args, ct);
            var enter = Describe(node, LifecycleReason.Open, null, node);
            try { await node.PrepareEnterAsync(enter, ct); }
            catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
            CompleteChange(enter, null, null, node, enter);
            return node;
        }, cancellationToken);

    public Task CloseAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("Close", async ct =>
        {
            if (node.Parent != this) throw new InvalidOperationException("The instance does not belong to this container.");
            var exit = Describe(node, LifecycleReason.Close, node, null);
            try { await node.PrepareExitAsync(exit, ct); }
            catch (Exception ex) { await Context.CleanupAsync(ex, () => { node.RestoreLocalState(true, true); return Task.CompletedTask; }); throw; }
            await FinishCloseAsync(node, () => CompleteChange(exit, node, exit, null, null));
            return true;
        }, cancellationToken);
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
                if (previous != null) await previous.PrepareExitAsync(exit!, ct);
                await node.PrepareEnterAsync(enter, ct);
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
        var current = Current ?? throw new InvalidOperationException("Cannot pop an empty stack.");
        var previous = _history.Count > 1 ? _history[_history.Count - 2] : null;
        var exit = Describe(current, LifecycleReason.Close, current, previous);
        var enter = previous == null ? null : Describe(previous, LifecycleReason.Return, current, previous);
        try
        {
            await current.PrepareExitAsync(exit, ct);
            if (previous != null) await previous.PrepareEnterAsync(enter!, ct);
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            if (previous != null) await Context.CleanupAsync(ex, () => { previous.RestoreLocalState(false, false); return Task.CompletedTask; });
            await Context.CleanupAsync(ex, () => { current.RestoreLocalState(true, true); return Task.CompletedTask; });
            throw;
        }
        _history.RemoveAt(_history.Count - 1);
        await FinishCloseAsync(current, () => CompleteChange(exit, current, exit, previous, enter));
        return true;
    }, cancellationToken);

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
                if (previous != null) await previous.PrepareExitAsync(exit!, ct);
                await next.PrepareEnterAsync(enter, ct);
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

/// <summary>Topmost modal owns input. Resource must provide its own backdrop via the backend.</summary>
public class ModalContainerNode : ContainerNode
{
    private readonly List<UINode> _modals = new();
    private readonly Dictionary<UINode, object?> _focus = new();
    public UINode? Current => _modals.LastOrDefault();

    public Task<T> OpenAsync<T>(object? args = null, CancellationToken cancellationToken = default) where T : UINode
    {
        object? focus = null;
        return OperateAsync("OpenModal", async ct =>
        {
            var previous = Current;
            var node = await Context.CreateAsync<T>(this, args, ct);
            var enter = Describe(node, LifecycleReason.Open, previous, node);
            try
            {
                await node.PrepareEnterAsync(enter, ct);
                node.Element?.BringToFront();
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                throw;
            }
            _modals.Add(node); _focus.Add(node, focus);
            Context.SetInputOwner(node);
            CompleteChange(enter, null, null, node, enter, () => Context.Host.Focus(node.Element));
            return node;
        }, cancellationToken, () => focus = Context.Host.CaptureFocus());
    }

    public Task CloseAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("CloseModal", async ct =>
        {
            if (!ReferenceEquals(node, Current)) throw new InvalidOperationException("Only the current modal can be closed.");
            var focus = _focus[node];
            var exit = Describe(node, LifecycleReason.Close, node, _modals.Count > 1 ? _modals[_modals.Count - 2] : null);
            try { await node.PrepareExitAsync(exit, ct); }
            catch (Exception ex) { await Context.CleanupAsync(ex, () => { node.RestoreLocalState(true, true); return Task.CompletedTask; }); throw; }
            _modals.Remove(node); _focus.Remove(node);
            await FinishCloseAsync(node, () =>
            {
                Context.SetInputOwner(Current);
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
