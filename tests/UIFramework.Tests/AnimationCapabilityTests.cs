using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class AnimationCapabilityTests
{
    private static UIContext Create() { var host = new TestHost(); return new(host, host, host); }
    private static async Task<T> Attach<T>(UINode parent) where T : UINode, new()
    { var n = new T(); await n.AttachAsync(parent, typeof(T).Name, new MemoryElement()); return n; }
    private sealed class Recipe : IUITransition
    {
        public Func<TransitionContext, CancellationToken, Task>? Play;
        public Exception? RestoreFailure;
        public int Restored;
        public TransitionContext? LastContext;
        public IUITransitionRun Create(TransitionContext context) { LastContext = context; return new Run(this, context); }
        private sealed class Run(Recipe owner, TransitionContext context) : IUITransitionRun
        {
            public Task PlayAsync(CancellationToken token) => owner.Play?.Invoke(context, token) ?? Task.CompletedTask;
            public void Restore() { owner.Restored++; if (owner.RestoreFailure != null) throw owner.RestoreFailure; }
        }
    }
    private sealed class Preparation : NodeLifecycle
    {
        public TaskCompletionSource<bool>? Gate;
        public Action? Enter;
        public override Task EnteringAsync(LifecycleContext context, CancellationToken token)
        { Enter?.Invoke(); return Gate == null ? Task.CompletedTask : Gate.Task.WaitAsync(token); }
    }
    private sealed class OrderedElement(List<OrderedElement> siblings) : MemoryElement, IUIElement
    {
        public int Captures, Fronts;
        public new void BringToFront() { Fronts++; siblings.Remove(this); siblings.Add(this); }
        public new object CaptureOrder() { Captures++; return siblings.IndexOf(this); }
        public new void RestoreOrder(object order) { siblings.Remove(this); siblings.Insert((int)order, this); }
        public new ValueTask DisposeAsync() { siblings.Remove(this); return base.DisposeAsync(); }
    }
    private sealed class Loader : IUIResourceLoader
    {
        public readonly List<OrderedElement> Siblings = new();
        public Task<IUIResource> LoadAsync(string resource, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult<IUIResource>(new Resource(Siblings)); }
        private sealed class Resource(List<OrderedElement> siblings) : IUIResource
        {
            public IUIElement Instantiate(IUIElement? mount)
            { var e = new OrderedElement(siblings); siblings.Add(e); return e; }
            public void Dispose() { }
        }
    }

    [Fact] public async Task StateSeparatesPreparationPlaybackAndCompletionAndResetsForEachOperation()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reported = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var view = new CounterView(); view.AddLifecycle(new Preparation { Gate = gate });
        var states = new List<TransitionState>(); view.TransitionChanged += states.Add;
        var recipe = new Recipe { Play = async (c, ct) => { c.ReportProgress(.25f); reported.SetResult(true); await playing.Task.WaitAsync(ct); c.ReportProgress(.75f); } };
        view.Transition = recipe; ui.Register(() => view);
        var open = panels.OpenAsync<CounterView>();
        Assert.True(view.IsTransitioning); Assert.Equal(TransitionPhase.Enter, view.CurrentTransitionPhase);
        Assert.Equal(TransitionStage.Preparing, view.TransitionState!.Stage); Assert.Equal(0, view.TransitionProgress);
        gate.SetResult(true);
        await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TransitionStage.Playing, view.TransitionState!.Stage);
        playing.SetResult(true); await open;
        Assert.False(view.IsTransitioning); Assert.Null(view.CurrentTransitionPhase); Assert.Equal(1, view.TransitionProgress);
        Assert.Equal(new[] { TransitionStage.Preparing, TransitionStage.Playing, TransitionStage.Playing,
            TransitionStage.Playing, TransitionStage.Completed }, states.Select(s => s.Stage));
        Assert.Equal(new[] { 0f, 0f, .25f, .75f, 1f }, states.Select(s => s.Progress));
        Assert.All(states, s => Assert.Same(view, s.Context.Node)); Assert.Equal(1, recipe.Restored);
        states.Clear(); await panels.HideAsync(view, playAnimation: false);
        Assert.Equal(new[] { TransitionStage.Preparing, TransitionStage.Completed }, states.Select(s => s.Stage));
        Assert.Equal(TransitionPhase.Exit, states[0].Context.Phase); Assert.Equal(1, recipe.Restored);
        Assert.Throws<InvalidOperationException>(() => recipe.LastContext!.ReportProgress(.8f));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task FailureAndCancellationDoNotReportSuccess(bool cancel)
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root);
        using var ct = new CancellationTokenSource(); var original = new IOException("animation failure");
        var view = new CounterView(); var states = new List<TransitionState>(); view.TransitionChanged += states.Add;
        var recipe = new Recipe { Play = (c, token) => { c.ReportProgress(.4f); if (cancel) { ct.Cancel(); token.ThrowIfCancellationRequested(); } throw original; } };
        view.Transition = recipe; ui.Register(() => view);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => panels.OpenAsync<CounterView>(cancellationToken: ct.Token));
        if (!cancel) Assert.Same(original, error); else Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Equal(cancel ? TransitionStage.Canceled : TransitionStage.Failed, view.TransitionState!.Stage);
        Assert.False(view.IsTransitioning); Assert.Equal(.4f, view.TransitionProgress);
        Assert.DoesNotContain(states, s => s.Stage == TransitionStage.Completed);
        Assert.Equal(1, recipe.Restored); Assert.Empty(panels.Children);
    }

    [Theory] [InlineData(-.1f)] [InlineData(1.1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public async Task InvalidProgressFailsAndRestores(float progress)
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); var recipe = new Recipe
        { Play = (c, _) => { c.ReportProgress(progress); return Task.CompletedTask; } };
        var node = new CounterView { Transition = recipe }; ui.Register(() => node);
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => panels.OpenAsync<CounterView>());
        Assert.Contains(error, ((TestHost)ui.Log).Errors); Assert.Equal(1, recipe.Restored);
        Assert.False(node.IsTransitioning); Assert.Equal(TransitionStage.Failed, node.TransitionState!.Stage);
    }

    [Fact] public async Task BackwardsProgressAndProgressDuringPreparationAreErrors()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root);
        var recipe = new Recipe { Play = (c, _) => { c.ReportProgress(.6f); c.ReportProgress(.5f); return Task.CompletedTask; } };
        ui.Register(() => new CounterView { Transition = recipe });
        await Assert.ThrowsAsync<InvalidOperationException>(() => panels.OpenAsync<CounterView>());
        panels.ChildTransition = Transitions.Select(c => { c.ReportProgress(.5f); return new InstantTransition(); });
        ui.Register<UINode>(() => new UINode()); await Assert.ThrowsAsync<InvalidOperationException>(() => panels.OpenAsync<UINode>());
    }

    [Theory] [InlineData("preparing")] [InlineData("progress")] [InlineData("completed")] [InlineData("mutation")]
    public async Task ObserverFailureIsVisibleAndBothItemsFinish(string where)
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView());
        var first = await stack.PushAsync<CounterView>(); var original = new IOException("observer failure");
        first.TransitionChanged += state =>
        {
            if (where == "mutation") ui.DefaultTransition = new InstantTransition();
            if (where == "preparing" && state.Stage == TransitionStage.Preparing
                || where == "progress" && state.Stage == TransitionStage.Playing && state.Progress == .5f
                || where == "completed" && state.Stage == TransitionStage.Completed) throw original;
        };
        var recipe = new Recipe { Play = (c, _) => { c.ReportProgress(.5f); return Task.CompletedTask; } };
        stack.ChildTransition = recipe;
        var error = await Assert.ThrowsAnyAsync<Exception>(() => stack.PushAsync<CounterView>());
        if (where == "mutation") Assert.IsType<InvalidOperationException>(error); else Assert.Same(original, error);
        Assert.Same(first, stack.Current); Assert.Single(stack.History); Assert.True(first.IsInteractive);
        Assert.False(first.IsTransitioning); Assert.Equal(TransitionStage.Failed, first.TransitionState!.Stage);
        Assert.Contains(error, ((TestHost)ui.Log).Errors);
    }

    [Fact] public async Task FinishObserverFailureDoesNotHidePlaybackFailure()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); var original = new IOException("play");
        var cleanup = new ArgumentException("failed notification"); var view = new CounterView
        { Transition = new Recipe { Play = (_, _) => throw original } };
        view.TransitionChanged += s => { if (s.Stage == TransitionStage.Failed) throw cleanup; };
        ui.Register(() => view);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>()));
        Assert.Contains(cleanup, UIFailure.GetSecondaryExceptions(original)); Assert.False(view.IsTransitioning);
    }

    [Fact] public async Task RestoreFailureDoesNotProduceSuccessfulCompletion()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root);
        var original = new IOException("restore"); var recipe = new Recipe { RestoreFailure = original };
        var node = new CounterView { Transition = recipe }; var states = new List<TransitionState>(); node.TransitionChanged += states.Add;
        ui.Register(() => node);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>()));
        Assert.DoesNotContain(states, s => s.Stage == TransitionStage.Completed);
        Assert.Equal(TransitionStage.Failed, node.TransitionState!.Stage); Assert.Equal(1, recipe.Restored);
    }

    [Theory] [InlineData(0, 0, false)] [InlineData(10, 0, true)] [InlineData(0, 10, false)]
    public async Task DrawOrderOverridesPushAndPopAndRestoresSiblingOrder(int firstOrder, int secondOrder, bool firstOnTop)
    {
        var host = new TestHost(); var loader = new Loader(); await using var ui = new UIContext(host, loader, host);
        var stack = await Attach<StackContainerNode>(ui.Root); var count = 0;
        ui.Register(() => new CounterView { TransitionOrder = count++ == 0 ? firstOrder : secondOrder }, "page");
        var first = await stack.PushAsync<CounterView>(playAnimation: false);
        var recipe = new Recipe { Play = (c, _) =>
        {
            Assert.Same(firstOnTop ? first.Element : c.Direction == TransitionDirection.Forward ? c.Target!.Element : c.Source!.Element,
                loader.Siblings[^1]); return Task.CompletedTask;
        } };
        stack.ChildTransition = recipe; var second = await stack.PushAsync<CounterView>();
        Assert.Equal(new[] { first.Element, second.Element }, loader.Siblings.Cast<IUIElement>());
        await stack.PopAsync(); Assert.Single(loader.Siblings); Assert.Same(first.Element, loader.Siblings[0]);
    }

    [Fact] public async Task InstantSwitchDoesNotCaptureOrChangeTemporaryDrawingOrder()
    {
        var host = new TestHost(); await using var ui = new UIContext(host, host, host);
        var tabs = await Attach<TabContainerNode>(ui.Root); var siblings = new List<OrderedElement>();
        var a = new OrderedElement(siblings); var b = new OrderedElement(siblings); siblings.Add(a); siblings.Add(b);
        await tabs.BindAsync("A", new UINode { TransitionOrder = 10 }, a);
        await tabs.BindAsync("B", new UINode(), b);
        await tabs.SwitchAsync("A", false); await tabs.SwitchAsync("B", false);
        Assert.Equal(0, a.Captures + b.Captures + a.Fronts + b.Fronts); Assert.Equal(new[] { a, b }, siblings);
        Assert.Equal(TransitionStage.Completed, tabs.Current!.TransitionState!.Stage);
    }

    [Fact] public async Task DrawingOrderIsFrozenBeforeLifecyclePreparation()
    {
        var host = new TestHost(); var loader = new Loader(); await using var ui = new UIContext(host, loader, host);
        var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView(), "first"); var first = await stack.PushAsync<CounterView>(playAnimation: false);
        first.TransitionOrder = 10; var second = new ArgumentView();
        second.AddLifecycle(new Preparation { Enter = () => first.TransitionOrder = -10 }); ui.Register(() => second, "second");
        stack.ChildTransition = new Recipe { Play = (_, _) => { Assert.Same(first.Element, loader.Siblings[^1]); return Task.CompletedTask; } };
        await stack.PushAsync<ArgumentView>("item"); Assert.Equal(-10, first.TransitionOrder);
    }
}
