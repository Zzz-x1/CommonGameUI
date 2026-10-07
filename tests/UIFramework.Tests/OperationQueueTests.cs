using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public sealed class OperationQueueTests
{
    private sealed class Gate : IUITransition, IUITransitionRun
    {
        public readonly TaskCompletionSource<bool> Started = new();
        public readonly TaskCompletionSource<bool> Finish = new();
        public Exception? Failure, RestoreFailure;
        public int Plays;
        public IUITransitionRun Create(TransitionContext context) => this;
        public async Task PlayAsync(CancellationToken token)
        { Plays++; Started.TrySetResult(true); await Finish.Task.WaitAsync(token); if (Failure != null) throw Failure; }
        public void Restore() { if (RestoreFailure != null) throw RestoreFailure; }
    }
    private class CallbackView : UINode
    {
        public Func<Task>? Enter, Initialize;
        protected override Task OnInitializeAsync(CancellationToken token) => Initialize?.Invoke() ?? Task.CompletedTask;
        protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token) => Enter?.Invoke() ?? Task.CompletedTask;
    }
    private sealed class AView : CallbackView { }
    private static UIContext Create() { var host = new TestHost(); return new(host, host, host); }
    private static async Task<T> Attach<T>(UINode parent) where T : UINode, new()
    { var node = new T(); await node.AttachAsync(parent, typeof(T).Name); return node; }

    [Fact] public async Task CompletionContinuationStartsNextOperationAndLocksInputImmediately()
    {
        await using var ui = Create(); var enter = new Gate(); var exit = new Gate();
        ui.Register(() => new CounterView { Transition = enter }); var panels = await Attach<PanelContainerNode>(ui.Root);
        var opening = panels.OpenAsync<CounterView>();
        async Task NextAsync()
        {
            var node = await opening; node.Transition = exit;
            var hiding = panels.HideAsync(node);
            Assert.False(node.IsInteractive); Assert.Equal(1, exit.Plays);
            var show = panels.ShowAsync(node, playAnimation: false); Assert.False(show.IsCompleted);
            exit.Finish.SetResult(true); await Task.WhenAll(hiding, show); Assert.True(node.IsInteractive);
        }
        var next = NextAsync(); enter.Finish.SetResult(true); await next;
    }

    [Fact] public async Task QueueContinuesAfterOrdinaryFailureAndOtherContainerIsIndependent()
    {
        await using var ui = Create(); var gate = new Gate { Failure = new IOException("animation") }; int creates = 0;
        ui.Register(() => new CounterView { Transition = ++creates == 1 ? gate : new InstantTransition() });
        var a = await Attach<PanelContainerNode>(ui.Root); var b = await Attach<PanelContainerNode>(ui.Root);
        var failed = a.OpenAsync<CounterView>(); var next = a.OpenAsync<CounterView>();
        Assert.False(next.IsCompleted); Assert.NotNull(await b.OpenAsync<CounterView>());
        gate.Finish.SetResult(true);
        Assert.Same(gate.Failure, await Assert.ThrowsAsync<IOException>(() => failed));
        Assert.NotNull(await next); Assert.Single(a.Children);
    }

    [Fact] public async Task PendingCancellationDoesNotRunOrDelayFollowingRequest()
    {
        await using var ui = Create(); ui.Register(() => new CounterView(), "page");
        var stack = await Attach<StackContainerNode>(ui.Root); var first = await stack.PushAsync<CounterView>();
        var gate = new Gate(); first.Transition = gate;
        var active = stack.PopAsync(); using var cancel = new CancellationTokenSource();
        var canceled = stack.PushAsync<CounterView>(cancellationToken: cancel.Token); var next = stack.PushAsync<CounterView>();
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.False(active.IsCompleted); gate.Finish.SetResult(true); await active; await next;
        Assert.Single(stack.History); Assert.Equal(2, ((TestHost)ui.Host).Loaded.Count);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DuplicateCloseSharesExecutionAndFollowerCancellationOnlyCancelsItsWait(bool modal)
    {
        await using var ui = Create(); ui.Register(() => new CounterView(), "page");
        ContainerNode container = modal ? await Attach<ModalContainerNode>(ui.Root) : await Attach<PanelContainerNode>(ui.Root);
        var node = modal ? await ((ModalContainerNode)container).OpenAsync<CounterView>() : await ((PanelContainerNode)container).OpenAsync<CounterView>();
        var gate = new Gate(); node.Transition = gate;
        Task Close(CancellationToken token = default) => modal ? ((ModalContainerNode)container).CloseAsync(node, cancellationToken: token) : ((PanelContainerNode)container).CloseAsync(node, cancellationToken: token);
        var first = Close(); using var cancel = new CancellationTokenSource(); var follower = Close(cancel.Token); var joined = Close();
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => follower);
        Assert.False(first.IsCompleted); Assert.Equal(1, gate.Plays);
        gate.Finish.SetResult(true); await Task.WhenAll(first, joined); await Close();
        Assert.Equal(1, node.Exited); Assert.Equal(NodeState.Disposed, node.State);
    }

    [Fact] public async Task DuplicateClosePreservesOriginalFailureThenAllowsExplicitRetry()
    {
        await using var ui = Create(); ui.Register(() => new CounterView(), "page"); var panels = await Attach<PanelContainerNode>(ui.Root);
        var node = await panels.OpenAsync<CounterView>(); var gate = new Gate { Failure = new IOException("close") }; node.Transition = gate;
        var a = panels.CloseAsync(node); var b = panels.CloseAsync(node); gate.Finish.SetResult(true);
        Assert.Same(gate.Failure, await Assert.ThrowsAsync<IOException>(() => a));
        Assert.Same(gate.Failure, await Assert.ThrowsAsync<IOException>(() => b)); Assert.True(node.IsActive);
        node.Transition = new InstantTransition(); await panels.CloseAsync(node);
    }

    [Fact] public async Task QueuedOldTargetsCannotChangeCachedReopening()
    {
        await using var ui = Create(); ui.Register(() => new CounterView(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui.Root); var node = await panels.OpenAsync<CounterView>();
        var gate = new Gate(); node.Transition = gate;
        var close = panels.CloseAsync(node); var reopen = panels.OpenAsync<CounterView>(playAnimation: false);
        var oldHide = panels.HideAsync(node); var duplicateClose = panels.CloseAsync(node);
        gate.Finish.SetResult(true); await close; Assert.Same(node, await reopen);
        await Assert.ThrowsAsync<InvalidOperationException>(() => oldHide); await duplicateClose;
        Assert.True(node.IsActive); Assert.True(panels.TryGet<CounterView>(out _));
    }

    [Fact] public async Task TargetStateOperationsAndEmptyPopAreIdempotent()
    {
        await using var ui = Create(); ui.Register(() => new CounterView(), "page");
        var panels = await Attach<PanelContainerNode>(ui.Root); var window = await panels.OpenAsync<CounterView>();
        await panels.ShowAsync(window); await panels.HideAsync(window); await panels.HideAsync(window); await panels.ShowAsync(window);
        Assert.Equal(2, window.Entered); Assert.Equal(1, window.Exited);
        var stack = await Attach<StackContainerNode>(ui.Root); await stack.PopAsync();
        var page = await stack.PushAsync<CounterView>(); await stack.PopToAsync(page); Assert.Equal(1, page.Entered);
        await Task.WhenAll(stack.PopAsync(), stack.PopAsync()); Assert.Empty(stack.History);
        var tabs = await Attach<TabContainerNode>(ui.Root); var item = new CounterView(); await tabs.BindAsync("A", item);
        await tabs.SwitchAsync("A"); await tabs.SwitchAsync("A"); await tabs.HideAsync(); await tabs.HideAsync();
        Assert.Equal(1, item.Entered); Assert.Equal(1, item.Exited);
    }

    [Fact] public async Task QueuedTypedTabSwitchResolvesAfterBindCompletes()
    {
        await using var ui = Create(); var tabs = await Attach<TabContainerNode>(ui.Root); var gate = new TaskCompletionSource<bool>();
        var item = new CallbackView { Initialize = () => gate.Task };
        var bind = tabs.BindAsync("A", item); var select = tabs.SwitchAsync<CallbackView>();
        Assert.False(select.IsCompleted); gate.SetResult(true); await Task.WhenAll(bind, select); Assert.Same(item, tabs.Current);
    }

    [Fact] public async Task LifecycleCanOperateChildContainerButCannotEnqueueOnItsAncestor()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root);
        var view = new CallbackView(); ui.Register(() => view);
        view.Enter = async () =>
        {
            var child = await Attach<TabContainerNode>(view); await child.BindAsync("A", new UINode()); await child.SwitchAsync("A");
            await Assert.ThrowsAsync<InvalidOperationException>(() => stack.PopAsync());
        };
        await stack.PushAsync<CallbackView>(); Assert.Same(view, stack.Current);
    }

    [Fact] public async Task CompletedLifecycleFrameDoesNotRejectLaterContinuation()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root);
        var signal = new TaskCompletionSource<bool>(); Task later = Task.CompletedTask;
        async Task ContinueAsync() { await signal.Task; await panels.HideAsync(panels.Children.Single()); }
        ui.Register(() => new CallbackView { Enter = () => { later = ContinueAsync(); return Task.CompletedTask; } });
        var node = await panels.OpenAsync<CallbackView>(); signal.SetResult(true); await later;
        Assert.False(node.IsVisible);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CrossContainerWaitCycleFailsInsteadOfDeadlocking(bool canceledPredecessor)
    {
        await using var ui = Create(); var a = await Attach<PanelContainerNode>(ui.Root); var b = await Attach<PanelContainerNode>(ui.Root);
        var entered = new TaskCompletionSource<bool>(); var resume = new TaskCompletionSource<bool>(); int creates = 0;
        ui.Register(() => new CallbackView { Enter = ++creates == 1 ? async () => { entered.SetResult(true); await resume.Task; await a.OpenAsync<CounterView>(); } : null });
        ui.Register(() => new CounterView { EnterFailure = null });
        var first = b.OpenAsync<CallbackView>(); await entered.Task;
        using var canceled = new CancellationTokenSource();
        Task? skipped = canceledPredecessor ? b.OpenAsync<CallbackView>(cancellationToken: canceled.Token) : null;
        var aView = new AView { Enter = async () => await b.OpenAsync<CallbackView>() };
        ui.Register(() => aView);
        var second = a.OpenAsync<AView>();
        if (skipped != null) { canceled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => skipped); }
        resume.SetResult(true);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Contains("Circular", failure.Message); await second;
    }

    [Fact] public async Task DisposalCancelsActiveAndPendingAndWaitsForDrain()
    {
        var ui = Create(); ui.Register(() => new CounterView(), "page"); var stack = await Attach<StackContainerNode>(ui.Root);
        var host = (TestHost)ui.Host; host.LoadGate = new(); var active = stack.PushAsync<CounterView>(); var pending = stack.PushAsync<CounterView>();
        await ui.DisposeAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(NodeState.Disposed, stack.State); Assert.Empty(host.Loaded);
    }

    [Fact] public async Task RestorationFailureBlocksQueuedAndNewRequestsUntilDisposal()
    {
        await using var ui = Create(); var gate = new Gate { Failure = new IOException("play"), RestoreFailure = new IOException("restore") };
        ui.Register(() => new CounterView { Transition = gate }); var panels = await Attach<PanelContainerNode>(ui.Root);
        var failed = panels.OpenAsync<CounterView>(); var queued = panels.OpenAsync<CounterView>(); gate.Finish.SetResult(true);
        Assert.Same(gate.Failure, await Assert.ThrowsAsync<IOException>(() => failed));
        Assert.Same(gate.Failure, await Assert.ThrowsAsync<IOException>(() => queued));
        Assert.Same(gate.Failure, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>()));
        Assert.Contains(gate.RestoreFailure, UIFailure.GetSecondaryExceptions(gate.Failure!));
    }
}
