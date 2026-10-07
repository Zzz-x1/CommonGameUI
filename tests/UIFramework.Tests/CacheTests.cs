using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public sealed class CacheTests
{
    private sealed class Page : UINode<string>
    {
        public int Initializations, Disposals;
        public Exception? EnterFailure;
        public bool CancelEnter;
        public List<LifecycleContext> Enters = new();
        protected override Task OnInitializeAsync(CancellationToken token) { Initializations++; return Task.CompletedTask; }
        protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
        {
            Enters.Add(context);
            if (EnterFailure != null) throw EnterFailure;
            if (CancelEnter) throw new OperationCanceledException(token);
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { Disposals++; return Task.CompletedTask; }
    }
    private sealed class Dialog : DialogNode<bool> { }
    private sealed class Backdrop : MemoryElement, IUIBackdrop
    {
        public Task ShowAsync(bool animate, CancellationToken token) => Task.CompletedTask;
        public Task HideAsync(bool animate, CancellationToken token) => Task.CompletedTask;
        public void Restore(bool visible) => SetVisible(visible);
    }
    private sealed class Backdrops : IUIBackdropFactory
    {
        public List<Backdrop> Instances = new();
        public Task<IUIBackdrop> CreateAsync(IUIElement? mount, Func<Task> dismiss, CancellationToken token)
        { var b = new Backdrop(); Instances.Add(b); return Task.FromResult<IUIBackdrop>(b); }
    }
    private sealed class DeferredPosts : SynchronizationContext
    {
        private readonly Queue<Action> _posts = new();
        public override void Post(SendOrPostCallback callback, object? state) => _posts.Enqueue(() => callback(state));
        public void Flush() { while (_posts.Count != 0) _posts.Dequeue()(); }
    }
    private static UIContext Context() { var host = new TestHost(); return new(host, host, host); }
    private static async Task<T> Attach<T>(UINode parent) where T : UINode, new()
    { var node = new T(); await node.AttachAsync(parent, typeof(T).Name); return node; }

    [Fact] public async Task DefaultCloseStillDestroys()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), "page");
        var panels = await Attach<PanelContainerNode>(ui.Root);
        var page = await panels.OpenAsync<Page>("first"); await panels.CloseAsync(page);
        Assert.Equal(NodeState.Disposed, page.State); Assert.NotSame(page, await panels.OpenAsync<Page>("second"));
    }

    [Fact] public async Task PanelCachePreservesSubtreeAndReappliesParameters()
    {
        await using var ui = Context(); int configured = 0;
        ui.Register<Page>(() => new Page(), "page", configure: _ => configured++, cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var page = await panels.OpenAsync<Page>("A");
        var tabs = await Attach<TabContainerNode>(page); var child = new UINode();
        await tabs.BindAsync("Equipment", child); await tabs.SwitchAsync("Equipment");
        await panels.CloseAsync(page);
        Assert.True(page.IsCached); Assert.True(child.IsCached); Assert.False(child.IsInteractive);
        Assert.False(page.Lifetime.IsCancellationRequested); Assert.False(panels.TryGet<Page>(out _));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tabs.HideAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new UINode().AttachAsync(page, "invalid"));
        Assert.Same(page, await panels.OpenAsync<Page>("B")); Assert.Equal("B", page.Parameters);
        Assert.Equal("Equipment", tabs.CurrentName); Assert.Same(child, tabs.Current);
        Assert.False(child.IsCached); Assert.True(child.IsInteractive);
        Assert.Equal(1, configured); Assert.Equal(1, page.Initializations);
        Assert.False(page.Enters[0].IsCacheReuse); Assert.True(page.Enters[1].IsCacheReuse);
        Assert.False(page.Enters[1].IsFirstEnter); Assert.Single(((TestHost)ui.Host).Loaded);
    }

    [Fact] public async Task HiddenPanelIsNotACacheEntry()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var page = await panels.OpenAsync<Page>("A");
        await panels.HideAsync(page); await panels.ClearCacheAsync();
        Assert.False(page.IsCached); Assert.True(panels.TryGet<Page>(out _));
        Assert.Same(page, await panels.OpenAsync<Page>());
        await panels.HideAsync(page); await panels.CloseAsync(page);
        Assert.True(page.IsCached); Assert.Same(page, await panels.OpenAsync<Page>("B"));
    }

    [Fact] public async Task LastClosedMultipleInstanceReplacesOldCache()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), "page", WindowInstanceMode.Multiple, cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root);
        var a = await panels.OpenAsync<Page>("A"); var b = await panels.OpenAsync<Page>("B");
        await panels.CloseAsync(a); await panels.CloseAsync(b);
        Assert.Equal(NodeState.Disposed, a.State); Assert.True(b.IsCached); Assert.Equal(1, a.Disposals);
        Assert.Same(b, await panels.OpenAsync<Page>("C"));
    }

    [Fact] public async Task CacheIsContainerLocalAndExplicitDisposalRemovesSlot()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var first = await Attach<PanelContainerNode>(ui.Root); var second = await Attach<PanelContainerNode>(ui.Root);
        var a = await first.OpenAsync<Page>("A"); await first.CloseAsync(a);
        var b = await second.OpenAsync<Page>("B"); Assert.NotSame(a, b);
        await a.DisposeAsync(); Assert.NotSame(a, await first.OpenAsync<Page>("C"));
    }

    [Fact] public async Task PopToRetainsOnlyLastClosedTypeAndHistoryExcludesCache()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var stack = await Attach<StackContainerNode>(ui.Root);
        var a = await stack.PushAsync<Page>("A"); var b = await stack.PushAsync<Page>("B"); var c = await stack.PushAsync<Page>("C");
        await stack.PopToAsync(a); Assert.Same(a, Assert.Single(stack.History));
        Assert.True(b.IsCached); Assert.Equal(NodeState.Disposed, c.State);
        Assert.Same(b, await stack.PushAsync<Page>("D")); Assert.Equal("D", b.Parameters);
        await stack.PopAsync(); await stack.PopAsync(); Assert.Empty(stack.History);
        Assert.True(a.IsCached); Assert.Equal(NodeState.Disposed, b.State);
    }

    [Theory] [InlineData(ModalBackdropMode.Shared)] [InlineData(ModalBackdropMode.PerModal)]
    public async Task ModalCacheReleasesBackdropsAndRestoresFocus(ModalBackdropMode mode)
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var host = (TestHost)ui.Host; var focus = new object(); host.Focus = focus;
        var modals = await Attach<ModalContainerNode>(ui.Root); var backdrops = new Backdrops(); modals.ConfigureBackdrop(backdrops, mode);
        var a = await modals.OpenAsync<Page>("A"); var b = await modals.OpenAsync<Page>("B");
        await modals.CloseToAsync(a); Assert.Same(a, modals.Current); Assert.Same(a, ui.InputOwner);
        Assert.True(b.IsCached); Assert.True(a.IsInteractive);
        await modals.CloseAsync(a); Assert.Null(modals.Current); Assert.Null(ui.InputOwner); Assert.Same(focus, host.Focus);
        Assert.All(backdrops.Instances, d => Assert.Equal(1, d.Disposals));
        Assert.Same(a, await modals.OpenAsync<Page>("C")); Assert.False(a.IsCached);
        Assert.Equal(mode == ModalBackdropMode.Shared ? 2 : 3, backdrops.Instances.Count);
    }

    [Fact] public async Task DialogCloseCancelsOnlyItsOwnSessionAndCanBeReused()
    {
        await using var ui = Context(); ui.Register<Dialog>(() => new Dialog(), cacheMode: NodeCacheMode.KeepAlive);
        var modals = await Attach<ModalContainerNode>(ui.Root);
        var first = modals.ShowDialogAsync<Dialog, bool>(); var dialog = (Dialog)modals.Current!;
        await modals.CloseAsync(dialog); var second = modals.ShowDialogAsync<Dialog, bool>();
        Assert.Same(dialog, modals.Current); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(second.IsCompleted); Assert.True(dialog.IsInteractive);
        dialog.Complete(false); Assert.False(await second);
        var third = modals.ShowDialogAsync<Dialog, bool>(); Assert.Same(dialog, modals.Current);
        Assert.False(third.IsCompleted); dialog.Complete(true); Assert.True(await third); Assert.True(dialog.IsCached);
    }

    [Fact] public async Task DelayedPostedResultAndOldShowDialogCannotAffectNewSession()
    {
        var posts = new DeferredPosts(); var previous = SynchronizationContext.Current;
        UIContext ui;
        try { SynchronizationContext.SetSynchronizationContext(posts); ui = Context(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        await using var lifetime = ui;
        ui.Register<Dialog>(() => new Dialog(), cacheMode: NodeCacheMode.KeepAlive);
        var modals = await Attach<ModalContainerNode>(ui.Root);
        var first = modals.ShowDialogAsync<Dialog, bool>(); var dialog = (Dialog)modals.Current!;
        dialog.Complete(true); await modals.CloseAsync(dialog);
        var second = modals.ShowDialogAsync<Dialog, bool>(); Assert.Same(dialog, modals.Current);
        posts.Flush(); Assert.True(await first); Assert.False(second.IsCompleted); Assert.True(dialog.IsInteractive);
        dialog.Complete(false); posts.Flush(); Assert.False(await second);
    }

    [Fact] public async Task OldAnsweredWaiterCannotCloseTheReopenedDialog()
    {
        await using var ui = Context(); ui.Register<Dialog>(() => new Dialog(), cacheMode: NodeCacheMode.KeepAlive);
        var modals = await Attach<ModalContainerNode>(ui.Root); var first = await modals.OpenAsync<Dialog>();
        var result = first.WaitForResultAsync(default); first.Complete(true);
        await modals.CloseAsync(first); var next = await modals.OpenAsync<Dialog>(); var pending = next.WaitForResultAsync(default);
        Assert.True(await result); Assert.False(pending.IsCompleted); next.Complete(false); Assert.False(await pending);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task FailedOrCanceledReuseDestroysInstance(bool cancel)
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var page = await panels.OpenAsync<Page>("A"); await panels.CloseAsync(page);
        var failure = new IOException("prepare"); page.CancelEnter = cancel; if (!cancel) page.EnterFailure = failure;
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => panels.OpenAsync<Page>("B"));
        else Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<Page>("B")));
        Assert.Equal(NodeState.Disposed, page.State); Assert.Empty(panels.Children);
        Assert.NotSame(page, await panels.OpenAsync<Page>("C"));
    }

    [Fact] public async Task InvalidReuseParametersDestroyCacheRatherThanKeepDirtyInstance()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var page = await panels.OpenAsync<Page>("A"); await panels.CloseAsync(page);
        await Assert.ThrowsAsync<ArgumentException>(() => panels.OpenAsync<Page>(123)); Assert.Equal(NodeState.Disposed, page.State);
    }

    [Fact] public async Task ClearCachesReleasesResourcesIncludingNestedCachedContainers()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), "page", cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var shell = await panels.OpenAsync<Page>("shell");
        var inner = await Attach<PanelContainerNode>(shell); var nested = await inner.OpenAsync<Page>("nested"); await inner.CloseAsync(nested);
        await panels.CloseAsync(shell); await ui.ClearCacheAsync();
        Assert.Equal(NodeState.Disposed, shell.State); Assert.Equal(NodeState.Disposed, nested.State);
        Assert.All(((TestHost)ui.Host).Resources, r => Assert.Equal(1, r.Disposals));
        await panels.ClearCacheAsync<Page>(); await panels.ClearCacheAsync();
        var live = await panels.OpenAsync<Page>("live"); var hiddenInner = await Attach<PanelContainerNode>(live);
        var hidden = await hiddenInner.OpenAsync<Page>("hidden"); await hiddenInner.HideAsync(hidden);
        await ui.ClearCacheAsync(); Assert.Equal(NodeState.Ready, live.State); Assert.Equal(NodeState.Ready, hidden.State);
    }

    [Fact] public async Task RetainedNestedModalStopsOwningInputUntilItsShellReopens()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var shell = await panels.OpenAsync<Page>("shell");
        var nested = await Attach<ModalContainerNode>(shell); var dialog = await nested.OpenAsync<Page>("modal");
        await panels.CloseAsync(shell); Assert.Null(ui.InputOwner); Assert.True(dialog.IsCached);
        Assert.Same(shell, await panels.OpenAsync<Page>("again")); Assert.Same(dialog, ui.InputOwner);
        Assert.Same(dialog, nested.Current); Assert.True(dialog.IsInteractive);
    }

    [Fact] public async Task FailedCloseNotificationsDisposeRatherThanCache()
    {
        await using var ui = Context(); var error = new IOException("close notification");
        var lifecycle = new RecordingLifecycle { OnExited = _ => throw error };
        ui.Register<Page>(() => { var node = new Page(); node.AddLifecycle(lifecycle); return node; }, cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var page = await panels.OpenAsync<Page>("A");
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => panels.CloseAsync(page)));
        Assert.Equal(NodeState.Disposed, page.State); Assert.Empty(panels.Children);
    }

    [Fact] public async Task HiddenCachedShellReleasesNestedModalInput()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var shell = await panels.OpenAsync<Page>("shell");
        var nested = await Attach<ModalContainerNode>(shell); var dialog = await nested.OpenAsync<Page>("modal");
        await panels.HideAsync(shell); await panels.CloseAsync(shell);
        Assert.Null(ui.InputOwner); Assert.True(dialog.IsCached);
        await panels.OpenAsync<Page>("again"); Assert.Same(dialog, ui.InputOwner); Assert.True(dialog.IsInteractive);
    }

    [Fact] public async Task CacheReplacementFailureDisposesBothInstancesAndKeepsOriginalError()
    {
        await using var ui = Context(); ui.Register<Page>(() => new Page(), "page", WindowInstanceMode.Multiple, cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var a = await panels.OpenAsync<Page>("A"); var b = await panels.OpenAsync<Page>("B");
        await panels.CloseAsync(a); var error = new IOException("release old cache"); ((MemoryElement)a.Element!).DisposeFailure = error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => panels.CloseAsync(b)));
        Assert.Equal(NodeState.Disposed, a.State); Assert.Equal(NodeState.Disposed, b.State); Assert.Empty(panels.Children);
    }

    [Fact] public async Task ParentDisposalDoesNotRecacheChildren()
    {
        var ui = Context(); ui.Register<Page>(() => new Page(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var page = await panels.OpenAsync<Page>("A"); await panels.CloseAsync(page);
        var lifetime = page.Lifetime; await ui.DisposeAsync(); Assert.Equal(NodeState.Disposed, page.State); Assert.Equal(1, page.Disposals); Assert.True(lifetime.IsCancellationRequested);
    }
}
