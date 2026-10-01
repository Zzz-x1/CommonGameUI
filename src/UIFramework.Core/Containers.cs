namespace UIFramework;

public abstract class ContainerNode : UINode
{
    protected override bool InitiallyVisible => true;

    protected async Task<T> OperateAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token)
    {
        T result = default!;
        await RunAsync(operation, async ct =>
        {
            SetLocalState(true, false);
            Exception? primary = null;
            try { result = await action(ct); }
            catch (Exception ex) { primary = ex; throw; }
            finally
            {
                if (State == NodeState.Ready)
                {
                    if (primary == null) SetLocalState(true, true);
                    else await Context.CleanupAsync(primary, () => { SetLocalState(true, true); return Task.CompletedTask; });
                }
            }
        }, token);
        return result;
    }
}

/// <summary>Independent instances; no implicit history or single-instance lookup.</summary>
public class PanelContainerNode : ContainerNode
{
    public Task<T> OpenAsync<T>(object? args = null, CancellationToken cancellationToken = default) where T : UINode =>
        OperateAsync("Open", async ct =>
        {
            var node = await Context.CreateAsync<T>(this, args, ct);
            try { await node.EnterAsync(ct); return node; }
            catch (Exception ex) { await Context.CleanupAsync(ex, async () => await node.DisposeAsync()); throw; }
        }, cancellationToken);

    public Task CloseAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("Close", async ct =>
        {
            if (node.Parent != this) throw new InvalidOperationException("The instance does not belong to this container.");
            await node.ExitAsync(ct);
            await node.DisposeAsync();
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
            try
            {
                if (previous != null) await previous.ExitAsync(ct);
                await node.EnterAsync(ct);
                ct.ThrowIfCancellationRequested();
                _history.Add(node);
                return node;
            }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                if (previous != null) await Context.CleanupAsync(ex, () => { previous.SetLocalState(true, true); return Task.CompletedTask; });
                throw;
            }
        }, cancellationToken);

    public Task PopAsync(CancellationToken cancellationToken = default) => OperateAsync("Pop", async ct =>
    {
        var current = Current ?? throw new InvalidOperationException("Cannot pop an empty stack.");
        var previous = _history.Count > 1 ? _history[_history.Count - 2] : null;
        try
        {
            await current.ExitAsync(ct);
            if (previous != null) await previous.EnterAsync(ct);
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            await Context.CleanupAsync(ex, () =>
            {
                previous?.SetLocalState(false, false);
                current.SetLocalState(true, true);
                return Task.CompletedTask;
            });
            throw;
        }
        _history.RemoveAt(_history.Count - 1);
        await current.DisposeAsync();
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
            try
            {
                if (previous != null) await previous.ExitAsync(ct);
                await next.EnterAsync(ct);
                ct.ThrowIfCancellationRequested();
                CurrentName = name;
            }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, () =>
                {
                    next.SetLocalState(false, false);
                    previous?.SetLocalState(true, true);
                    return Task.CompletedTask;
                });
                throw;
            }
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

    public Task<T> OpenAsync<T>(object? args = null, CancellationToken cancellationToken = default) where T : UINode =>
        OperateAsync("OpenModal", async ct =>
        {
            var previous = Current;
            var focus = Context.Host.CaptureFocus();
            var node = await Context.CreateAsync<T>(this, args, ct);
            try
            {
                previous?.SetLocalState(true, false);
                await node.EnterAsync(ct);
                node.Element?.BringToFront();
                ct.ThrowIfCancellationRequested();
                _modals.Add(node); _focus.Add(node, focus);
                Context.SetInputOwner(node);
                Context.Host.Focus(node.Element);
                return node;
            }
            catch (Exception ex)
            {
                await Context.CleanupAsync(ex, async () => await node.DisposeAsync());
                await Context.CleanupAsync(ex, () =>
                {
                    previous?.SetLocalState(true, true);
                    Context.Host.RestoreFocus(focus); return Task.CompletedTask;
                });
                throw;
            }
        }, cancellationToken);

    public Task CloseAsync(UINode node, CancellationToken cancellationToken = default) =>
        OperateAsync("CloseModal", async ct =>
        {
            if (!ReferenceEquals(node, Current)) throw new InvalidOperationException("Only the current modal can be closed.");
            var focus = _focus[node];
            await node.ExitAsync(ct);
            _modals.Remove(node); _focus.Remove(node);
            Context.SetInputOwner(Current);
            Exception? disposalFailure = null;
            try { await node.DisposeAsync(); }
            catch (Exception ex) { disposalFailure = ex; throw; }
            finally
            {
                if (disposalFailure == null)
                {
                    Current?.SetLocalState(true, true); Context.Host.RestoreFocus(focus);
                }
                else await Context.CleanupAsync(disposalFailure, () =>
                {
                    Current?.SetLocalState(true, true); Context.Host.RestoreFocus(focus); return Task.CompletedTask;
                });
            }
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
        _modals.Remove(child); _focus.Remove(child);
        if (ReferenceEquals(Context.InputOwner, child)) Context.SetInputOwner(Current);
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
