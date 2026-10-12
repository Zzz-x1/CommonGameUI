using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class WaitForCloseTests
{
    private static UIContext Create()
    {
        var host = new TestHost(); return new UIContext(host, host, host);
    }
    private static async Task<T> Attach<T>(UIContext ui) where T : UINode, new()
    {
        var node = new T(); await node.AttachAsync(ui.Root, typeof(T).Name, new MemoryElement()); return node;
    }
    private sealed class SlowDispose : CounterView
    {
        public readonly TaskCompletionSource<bool> Started = new();
        public readonly TaskCompletionSource<bool> Finish = new();
        protected override Task OnDisposeAsync() { Started.SetResult(true); return Finish.Task; }
    }
    private sealed class BrokenTransition(Exception error) : IUITransition, IUITransitionRun
    {
        public IUITransitionRun Create(TransitionContext context) => this;
        public Task PlayAsync(CancellationToken token) => Task.FromException(error);
        public void Restore() { }
    }
    private sealed class ExitGate : IUITransition, IUITransitionRun
    {
        public readonly TaskCompletionSource<bool> Finish = new();
        public IUITransitionRun Create(TransitionContext context) => this;
        public Task PlayAsync(CancellationToken token) => Finish.Task;
        public void Restore() { }
    }

    [Fact]
    public async Task ExitAnimationMustFinishBeforeWaitCompletes()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView());
        var panels = await Attach<PanelContainerNode>(ui); var window = await panels.OpenAsync<CounterView>();
        var gate = new ExitGate(); window.Transition = gate;
        var waiting = window.WaitForCloseAsync(); var closing = panels.CloseAsync(window);
        Assert.False(waiting.IsCompleted); Assert.False(closing.IsCompleted);
        gate.Finish.SetResult(true); await closing; await waiting;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task WaitIncludesCleanupForEveryWindowContainer(int kind)
    {
        await using var ui = Create(); ui.Register<SlowDispose>(() => new SlowDispose());
        ContainerNode container = kind switch
        {
            0 => await Attach<PanelContainerNode>(ui),
            1 => await Attach<StackContainerNode>(ui),
            _ => await Attach<ModalContainerNode>(ui)
        };
        var window = kind switch
        {
            0 => await ((PanelContainerNode)container).OpenAsync<SlowDispose>(),
            1 => await ((StackContainerNode)container).PushAsync<SlowDispose>(),
            _ => await ((ModalContainerNode)container).OpenAsync<SlowDispose>()
        };
        var waiting = window.WaitForCloseAsync();
        var closing = kind switch
        {
            0 => ((PanelContainerNode)container).CloseAsync(window),
            1 => ((StackContainerNode)container).PopAsync(),
            _ => ((ModalContainerNode)container).CloseAsync(window)
        };
        await window.Started.Task;
        Assert.False(waiting.IsCompleted);
        window.Finish.SetResult(true);
        await closing; await waiting;
        Assert.Equal(NodeState.Disposed, window.State);
        await window.WaitForCloseAsync();
    }

    [Fact]
    public async Task HideAndCoverDoNotCloseAnOpening()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); ui.Register<ArgumentView>(() => new ArgumentView());
        var panels = await Attach<PanelContainerNode>(ui);
        var panel = await panels.OpenAsync<CounterView>(); var hidden = panel.WaitForCloseAsync();
        await panels.HideAsync(panel); Assert.False(hidden.IsCompleted);
        await panels.ShowAsync(panel); Assert.False(hidden.IsCompleted);
        await panels.CloseAsync(panel); await hidden;
        var pages = await Attach<StackContainerNode>(ui);
        var page = await pages.PushAsync<CounterView>(); var covered = page.WaitForCloseAsync();
        await pages.PushAsync<ArgumentView>("details"); Assert.False(covered.IsCompleted);
        await pages.PopToAsync(page); Assert.False(covered.IsCompleted);
        await pages.PopAsync(); await covered;
    }

    [Fact]
    public async Task CachedReopeningHasAnIndependentWait()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView(), cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui); var window = await panels.OpenAsync<CounterView>();
        var first = window.WaitForCloseAsync(); await panels.CloseAsync(window);
        Assert.True(window.IsCached); await window.WaitForCloseAsync();
        Assert.Same(window, await panels.OpenAsync<CounterView>());
        var second = window.WaitForCloseAsync(); await first;
        Assert.False(second.IsCompleted); Assert.True(first.IsCompletedSuccessfully);
        await panels.CloseAsync(window); await second;
    }

    [Fact]
    public async Task CancelingWaitDoesNotCloseTheWindow()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView());
        var panels = await Attach<PanelContainerNode>(ui); var window = await panels.OpenAsync<CounterView>();
        using var cancel = new CancellationTokenSource(); var waiting = window.WaitForCloseAsync(cancel.Token);
        var other = window.WaitForCloseAsync(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.True(window.IsInteractive); Assert.False(other.IsCompleted);
        await panels.CloseAsync(window); await other;
    }

    [Fact]
    public async Task CancelingWaitDuringCleanupDoesNotCancelCleanup()
    {
        await using var ui = Create(); ui.Register<SlowDispose>(() => new SlowDispose());
        var panels = await Attach<PanelContainerNode>(ui); var window = await panels.OpenAsync<SlowDispose>();
        using var cancel = new CancellationTokenSource(); var waiting = window.WaitForCloseAsync(cancel.Token);
        var closing = panels.CloseAsync(window); await window.Started.Task; cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.False(closing.IsCompleted); window.Finish.SetResult(true); await closing;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DirectAndParentDisposalCompleteTheWaitAfterCleanup(bool parent)
    {
        var ui = Create(); ui.Register<SlowDispose>(() => new SlowDispose());
        var panels = await Attach<PanelContainerNode>(ui); var window = await panels.OpenAsync<SlowDispose>();
        var waiting = window.WaitForCloseAsync();
        var disposal = parent ? ui.DisposeAsync().AsTask() : window.DisposeAsync().AsTask();
        await window.Started.Task; Assert.False(waiting.IsCompleted);
        window.Finish.SetResult(true); await disposal; await waiting; await ui.DisposeAsync();
    }

    [Fact]
    public async Task FailedPreparationKeepsWaitingAndCleanupFailurePropagatesOriginal()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView());
        var panels = await Attach<PanelContainerNode>(ui); var window = await panels.OpenAsync<CounterView>();
        var waiting = window.WaitForCloseAsync(); var preparation = new IOException("exit");
        window.Transition = new BrokenTransition(preparation);
        Assert.Same(preparation, await Assert.ThrowsAsync<IOException>(() => panels.CloseAsync(window)));
        Assert.False(waiting.IsCompleted); Assert.True(window.IsInteractive);
        window.Transition = null;
        var cleanup = new IOException("cleanup"); window.DisposeFailure = cleanup;
        Assert.Same(cleanup, await Assert.ThrowsAsync<IOException>(() => panels.CloseAsync(window)));
        Assert.Same(cleanup, await Assert.ThrowsAsync<IOException>(() => waiting));
        Assert.Same(cleanup, await Assert.ThrowsAsync<IOException>(() => window.WaitForCloseAsync()));
    }

    [Fact]
    public async Task StructuralNodesWithoutAnOpeningRejectWait()
    {
        await using var ui = Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Root.WaitForCloseAsync());
    }
}
