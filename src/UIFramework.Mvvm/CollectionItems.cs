namespace UIFramework.Mvvm;

public interface ICollectionItemFactory
{ Task<IUINode> CreateAsync(CollectionItemContext context, CancellationToken cancellationToken); }

[Serializable]
public sealed class CollectionSourceMapping
{
    public string ParentSource = "";
    public string ItemSource = "";
}

public abstract class CollectionItemContext : IAsyncDisposable
{
    public IUINode Owner { get; }
    public object Item { get; }
    public int Index { get; }
    public bool IsPreview { get; }
    public IUIElement Mount { get; }
    private readonly IReadOnlyList<CollectionSourceMapping> _sources;
    private readonly string _name;
    private IUIResource? _resource;
    private IUIElement? _element;
    internal IUINode? CreatedNode { get; private set; }
    private Task? _dispose;
    protected CollectionItemContext(IUINode owner, object item, int index, bool preview, IUIElement mount,
        IReadOnlyList<CollectionSourceMapping> sources, string name)
    {
        if (sources.Any(s => string.IsNullOrWhiteSpace(s.ParentSource) || string.IsNullOrWhiteSpace(s.ItemSource) || s.ItemSource == "Item") || sources.GroupBy(s => s.ItemSource).Any(g => g.Count() != 1))
            throw new ArgumentException("Item source mappings require distinct names; Item is reserved.");
        Owner = owner; Item = item; Index = index; IsPreview = preview; Mount = mount; _sources = sources; _name = name;
    }
    public async Task<IUINode> LoadAsync(string resource, Action<IUINode>? configure = null, CancellationToken cancellationToken = default)
    {
        if (_resource != null || _element != null) throw new InvalidOperationException("A collection item factory may create one item only.");
        _resource = await Owner.Context.Loader.LoadAsync(resource, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return await AttachAsync(_resource.Instantiate(Mount), configure, cancellationToken);
    }
    protected abstract IUINode ResolveNode(IUIElement element);
    public async Task<IUINode> AttachAsync(IUIElement element, Action<IUINode>? configure, CancellationToken cancellationToken)
    {
        if (_element != null || _dispose != null) throw new InvalidOperationException("Collection item context is already used or disposed.");
        _element = element; element.SetVisible(false);
        cancellationToken.ThrowIfCancellationRequested();
        CreatedNode = ResolveNode(element);
        MvvmExtensions.Initializers.Add(CreatedNode.Node, node =>
        {
            node.SetViewModel("Item", Item);
            foreach (var source in _sources) node.SetViewModel(source.ItemSource, Owner.GetViewModel<object>(source.ParentSource));
            configure?.Invoke(node);
        });
        await CreatedNode.Node.AttachRetainedAsync(Owner, _name, element, cancellationToken, initiallyVisible: true);
        return CreatedNode;
    }
    public ValueTask DisposeAsync() { _dispose ??= CloseAsync(); return new ValueTask(_dispose); }
    protected virtual void ReleaseNativeDisplay() { }
    private async Task CloseAsync()
    {
        Exception? first = null;
        if (CreatedNode != null) MvvmExtensions.Initializers.Remove(CreatedNode.Node);
        if (CreatedNode != null && CreatedNode.State != NodeState.Detached)
            try { await CreatedNode.DisposeAsync(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        else if (_element != null)
            try { await _element.DisposeAsync(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        try { ReleaseNativeDisplay(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        try { _resource?.Dispose(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
}

/// <summary>Tracks logical slots independently of asynchronous native item creation.</summary>
public abstract class CollectionTargetBase : ICollectionTarget, IAsyncDisposable
{
    private sealed class Entry(CollectionItemContext context, CancellationToken ownerToken)
    {
        internal readonly CollectionItemContext Context = context;
        internal readonly CancellationTokenSource Cancellation = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        internal IUINode? Node;
        internal Task Completion = Task.CompletedTask;
    }
    private readonly IUINode _owner;
    private readonly List<Entry> _entries = new();
    private readonly List<Task> _tasks = new();
    private Task? _dispose;
    public event Action? Changed;
    protected CollectionTargetBase(IUINode owner) => _owner = owner;
    protected abstract CollectionItemContext CreateContext(object item, int index);
    protected abstract Task<IUINode> CreateItemAsync(CollectionItemContext context, CancellationToken token);
    protected abstract void Place(IUINode node, int index);
    protected abstract void Detach(IUINode node);
    public void Insert(int index, object item)
    {
        _owner.Context.Host.VerifyAccess();
        if (_dispose != null) throw new ObjectDisposedException(GetType().Name);
        index = index == int.MaxValue ? _entries.Count : index;
        var entry = new Entry(CreateContext(item, index), _owner.Lifetime); _entries.Insert(index, entry); _tasks.RemoveAll(t => t.IsCompletedSuccessfully);
        entry.Completion = CreateAsync(entry); _tasks.Add(entry.Completion); MvvmExtensions.Scope(_owner.Node).ObserveCollection(entry.Completion);
    }
    private async Task CreateAsync(Entry entry)
    {
        Exception? failure = null;
        try
        {
            var node = await CreateItemAsync(entry.Context, entry.Cancellation.Token);
            if (!ReferenceEquals(node, entry.Context.CreatedNode)) throw new InvalidOperationException("Item factories must create their node through CollectionItemContext.");
            entry.Cancellation.Token.ThrowIfCancellationRequested();
            entry.Node = node; Reorder();
        }
        catch (Exception error)
        {
            try { await entry.Context.DisposeAsync(); } catch (Exception cleanup) { failure = error; MvvmRuntime.Failure(ref failure, cleanup); }
            if (error is OperationCanceledException && entry.Cancellation.IsCancellationRequested && failure == null && error.Data[UIFailure.SecondaryExceptionsKey] == null) return;
            _owner.Context.GetMvvm().Report(error, "MVVM collection item creation"); throw;
        }
    }
    public void Remove(int index)
    { VerifyAvailable(); RemoveCore(index); }
    private void VerifyAvailable()
    { _owner.Context.Host.VerifyAccess(); if (_dispose != null) throw new ObjectDisposedException(GetType().Name); }
    private void RemoveCore(int index)
    {
        var entry = _entries[index]; _entries.RemoveAt(index); Exception? first = null;
        try { entry.Cancellation.Cancel(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        try { if (entry.Node != null && entry.Node.State != NodeState.Disposed) Detach(entry.Node); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        var task = ReleaseAsync(entry); _tasks.Add(task); MvvmExtensions.Scope(_owner.Node).ObserveCollection(task);
        try { Reorder(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        if (first != null) { _owner.Context.GetMvvm().Report(first, "MVVM collection removal"); System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw(); }
    }
    private static async Task ReleaseAsync(Entry entry)
    {
        Exception? first = null;
        try { await entry.Completion; } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        try { await entry.Context.DisposeAsync(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        entry.Cancellation.Dispose();
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    public void Move(int oldIndex, int newIndex)
    { VerifyAvailable(); var entry = _entries[oldIndex]; _entries.RemoveAt(oldIndex); _entries.Insert(newIndex, entry); Reorder(); }
    private void Reorder()
    {
        int index = 0;
        foreach (var entry in _entries) if (entry.Node?.State == NodeState.Ready) Place(entry.Node, index++);
        Changed?.Invoke();
    }
    public void Clear() { VerifyAvailable(); ClearCore(); }
    private void ClearCore()
    {
        Exception? first = null;
        for (int i = _entries.Count - 1; i >= 0; i--)
            try { RemoveCore(i); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    public void Dispose() { _owner.ObserveMvvmTask(DisposeAsync().AsTask()); }
    public ValueTask DisposeAsync()
    {
        if (_dispose != null) return new ValueTask(_dispose);
        var completion = new TaskCompletionSource<Task>(); _dispose = completion.Task.Unwrap(); completion.SetResult(CloseAsync()); return new ValueTask(_dispose);
    }
    private async Task CloseAsync()
    {
        Exception? first = null;
        try { ClearCore(); } catch (Exception error) { MvvmRuntime.Failure(ref first, error); }
        try { await Task.WhenAll(_tasks); } catch { foreach (var task in _tasks.Where(t => !t.IsCompletedSuccessfully)) try { await task; } catch (Exception error) { MvvmRuntime.Failure(ref first, error); } }
        finally { _tasks.Clear(); }
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
}
