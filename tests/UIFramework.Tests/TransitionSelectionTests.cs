using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class TransitionSelectionTests
{
    private static UIContext Create() { var host = new TestHost(); return new(host, host, host); }
    private static async Task<T> Attach<T>(UINode parent) where T : UINode, new()
    { var node = new T(); await node.AttachAsync(parent, typeof(T).Name, new MemoryElement()); return node; }
    private sealed class Recipe : IUITransition
    {
        public readonly List<TransitionContext> Created = new();
        public int Played, Restored;
        public IUITransitionRun Create(TransitionContext context) { Created.Add(context); return new Run(this); }
        private sealed class Run(Recipe owner) : IUITransitionRun
        {
            public Task PlayAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); owner.Played++; return Task.CompletedTask; }
            public void Restore() => owner.Restored++;
        }
    }
    private sealed class Preparation : NodeLifecycle
    {
        public Action? Enter;
        public TaskCompletionSource<bool>? Gate;
        public override Task EnteringAsync(LifecycleContext context, CancellationToken token)
        { Enter?.Invoke(); return Gate == null ? Task.CompletedTask : Gate.Task.WaitAsync(token); }
    }
    private sealed class Backdrop : MemoryElement, IUIBackdrop
    {
        public readonly List<bool> Modes = new();
        public Task ShowAsync(bool animate, CancellationToken token) { token.ThrowIfCancellationRequested(); Modes.Add(animate); Visible = true; return Task.CompletedTask; }
        public Task HideAsync(bool animate, CancellationToken token) { token.ThrowIfCancellationRequested(); Modes.Add(animate); Visible = false; return Task.CompletedTask; }
        public void Restore(bool visible) => Visible = visible;
    }
    private sealed class Factory : IUIBackdropFactory
    {
        public readonly List<Backdrop> Created = new();
        public Task<IUIBackdrop> CreateAsync(IUIElement? mount, Func<Task> dismiss, CancellationToken token)
        { token.ThrowIfCancellationRequested(); var b = new Backdrop(); Created.Add(b); return Task.FromResult<IUIBackdrop>(b); }
    }

    [Fact] public async Task ConstructionIsLazyAndStackPhasesHaveStableSourceTarget()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>();
        var contexts = new List<TransitionContext>(); var recipe = new Recipe();
        stack.ChildTransition = Transitions.Select(c => { contexts.Add(c); return recipe; });
        Assert.Empty(contexts);
        var a = await stack.PushAsync<CounterView>(); var b = await stack.PushAsync<CounterView>();
        await stack.PopAsync(); await stack.PopAsync();
        Assert.Equal(new[] { TransitionPhase.PushEnter, TransitionPhase.PushExit, TransitionPhase.PushEnter,
            TransitionPhase.PopExit, TransitionPhase.PopEnter, TransitionPhase.PopExit }, contexts.Select(c => c.Phase));
        Assert.Null(contexts[0].Source); Assert.Same(a, contexts[0].Target);
        Assert.Same(a, contexts[1].Node); Assert.Same(b, contexts[2].Node);
        Assert.All(contexts.Skip(1).Take(2), c => { Assert.Same(a, c.Source); Assert.Same(b, c.Target); });
        Assert.All(contexts.Skip(3).Take(2), c => { Assert.Same(b, c.Source); Assert.Same(a, c.Target); });
        Assert.Null(contexts[^1].Target); Assert.Equal(contexts.Count, recipe.Played); Assert.Equal(recipe.Played, recipe.Restored);
    }

    [Fact] public async Task PopToOnlySelectsTopAndDestination()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>();
        var a = await stack.PushAsync<CounterView>(); var skipped = await stack.PushAsync<CounterView>(); var top = await stack.PushAsync<CounterView>();
        var contexts = new List<TransitionContext>(); stack.ChildTransition = Transitions.Select(c => { contexts.Add(c); return new InstantTransition(); });
        await stack.PopToAsync(a);
        Assert.Equal(new[] { TransitionPhase.PopExit, TransitionPhase.PopEnter }, contexts.Select(c => c.Phase));
        Assert.All(contexts, c => { Assert.Same(top, c.Source); Assert.Same(a, c.Target); Assert.NotSame(skipped, c.Node); });
        Assert.Equal(NodeState.Disposed, skipped.State);
    }

    [Fact] public async Task SelectionPrecedesPreparationAndCreatesRunAfterIt()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); var chosen = new Recipe(); var other = new Recipe();
        var changed = false; var calls = 0; var node = new CounterView();
        node.Transition = Transitions.Select(_ => { calls++; Assert.False(changed); return changed ? other : chosen; });
        node.AddLifecycle(new Preparation { Enter = () => changed = true }); ui.Register(() => node);
        await panels.OpenAsync<CounterView>();
        Assert.True(changed); Assert.Equal(1, calls); Assert.Single(chosen.Created); Assert.Empty(other.Created);
    }

    [Fact] public async Task BothRecipesAreFrozenBeforeEitherPreparation()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>();
        var first = await stack.PushAsync<CounterView>(); var chosen = new Recipe(); var other = new Recipe(); var state = false;
        var behavior = new Preparation { Enter = () => state = true };
        var second = new ArgumentView(); second.AddLifecycle(behavior); ui.Register(() => second);
        stack.ChildTransition = Transitions.Select(_ => state ? other : chosen);
        await stack.PushAsync<ArgumentView>("item");
        Assert.Equal(2, chosen.Created.Count); Assert.Empty(other.Created); Assert.Same(first, chosen.Created[0].Node);
    }

    [Fact] public async Task SelectorsFollowWholeRecipeInheritanceAndCanReturnInstant()
    {
        await using var ui = Create(); var global = new Recipe(); var container = new Recipe(); var own = new Recipe();
        ui.DefaultTransition = Transitions.Select(_ => global);
        var panels = await Attach<PanelContainerNode>(ui.Root); panels.ChildTransition = Transitions.Select(_ => container);
        ui.Register(() => new CounterView { Transition = Transitions.Select(_ => own) }); ui.Register<ArgumentView>();
        var a = await panels.OpenAsync<CounterView>(); await panels.OpenAsync<ArgumentView>("item");
        Assert.Single(own.Created); Assert.Single(container.Created); Assert.Empty(global.Created);
        a.Transition = Transitions.Select(_ => new InstantTransition()); await panels.HideAsync(a); Assert.Single(own.Created);
        var other = await Attach<PanelContainerNode>(ui.Root); await other.OpenAsync<ArgumentView>("another"); Assert.Single(global.Created);
    }

    [Fact] public async Task SharedSelectorCanChooseNamesAndGeneralPhases()
    {
        await using var ui = Create(); var tabs = await Attach<TabContainerNode>(ui.Root);
        var a = new UINode(); var b = new UINode(); await tabs.BindAsync("A", a); await tabs.BindAsync("B", b);
        await tabs.SwitchAsync("A", playAnimation: false); var enter = new Recipe(); var exit = new Recipe();
        tabs.ChildTransition = Transitions.Select(c => c.Phase == TransitionPhase.Enter && c.Target?.Name == "B" ? enter : exit);
        await tabs.SwitchAsync("B"); Assert.Single(enter.Created); Assert.Single(exit.Created);
        Assert.Same(a, enter.Created[0].Source); Assert.Same(b, enter.Created[0].Target);
        Assert.Equal(TransitionPhase.Exit, exit.Created[0].Phase);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task PanelAndModalExposeEnterExit(bool modal)
    {
        await using var ui = Create(); ui.Register<CounterView>(); var recipe = new Recipe(); ui.DefaultTransition = Transitions.Select(_ => recipe);
        if (modal) { var c = await Attach<ModalContainerNode>(ui.Root); var n = await c.OpenAsync<CounterView>(); await c.CloseAsync(n); }
        else { var c = await Attach<PanelContainerNode>(ui.Root); var n = await c.OpenAsync<CounterView>(); await c.HideAsync(n); await c.ShowAsync(n); await c.CloseAsync(n); }
        Assert.All(recipe.Created, c => Assert.Equal(c.Entering ? TransitionPhase.Enter : TransitionPhase.Exit, c.Phase));
        Assert.Equal(modal ? 2 : 4, recipe.Created.Count);
    }

    [Theory] [InlineData("throw")] [InlineData("null")] [InlineData("cycle")] [InlineData("mutate")]
    public async Task SelectionErrorsPreserveStackAndNeverUseOtherRecipes(string mode)
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>();
        var first = await stack.PushAsync<CounterView>(); var spare = new Recipe(); ui.DefaultTransition = spare;
        var original = new IOException("selector"); IUITransition? selector = null;
        selector = Transitions.Select(c => mode switch
        {
            "throw" => throw original,
            "null" => null!,
            "cycle" => selector!,
            "mutate" => Mutate(),
            _ => throw new InvalidOperationException()
        });
        IUITransition Mutate() { ui.DefaultTransition = new InstantTransition(); return spare; }
        stack.ChildTransition = selector;
        var error = await Assert.ThrowsAnyAsync<Exception>(() => stack.PushAsync<CounterView>());
        if (mode == "throw") Assert.Same(original, error); else Assert.IsType<InvalidOperationException>(error);
        Assert.Same(first, stack.Current); Assert.Single(stack.Children); Assert.True(first.IsInteractive);
        Assert.Empty(spare.Created); Assert.Contains(error, ((TestHost)ui.Log).Errors);
    }

    [Fact] public async Task NestedSelectionEvaluatesEachWrapperOnce()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); ui.Register<CounterView>();
        var innerCalls = 0; var outerCalls = 0; var recipe = new Recipe();
        var inner = Transitions.Select(_ => { innerCalls++; return recipe; });
        panels.ChildTransition = Transitions.Select(_ => { outerCalls++; return inner; });
        await panels.OpenAsync<CounterView>(); Assert.Equal(1, innerCalls); Assert.Equal(1, outerCalls); Assert.Single(recipe.Created);
    }

    [Fact] public void NullSelectionDelegateIsInvalid() => Assert.Throws<ArgumentNullException>(() => Transitions.Select(null!));

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task InstantOpenStillAwaitsResourceAndExposesFailure(bool fail)
    {
        var host = new TestHost { LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var ui = new UIContext(host, host, host); ui.Register<CounterView>("window");
        var panels = await Attach<PanelContainerNode>(ui.Root);
        ui.DefaultTransition = Transitions.Select(_ => throw new Exception("Unexpected animation"));
        var opening = panels.OpenAsync<CounterView>(playAnimation: false);
        Assert.False(opening.IsCompleted); Assert.Empty(panels.Children);
        var original = new IOException("Resource load failure");
        if (fail) host.LoadFailure = original;
        host.LoadGate.SetResult(true);
        if (fail)
        {
            Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => opening));
            Assert.Contains(original, host.Errors); Assert.Empty(panels.Children); Assert.Empty(host.Loaded);
        }
        else
        {
            var node = await opening; Assert.True(node.IsVisible); Assert.True(node.IsInteractive);
            Assert.Equal(1, node.Initialized); Assert.Equal(1, node.Entered); Assert.Single(host.Loaded);
        }
    }

    [Fact] public async Task FalseSkipsAllSelectorsAcrossContainerOperationsButRunsLifecycle()
    {
        await using var ui = Create(); ui.Register<CounterView>();
        ui.DefaultTransition = Transitions.Select(_ => throw new IOException("Must never select"));
        var panels = await Attach<PanelContainerNode>(ui.Root); var window = await panels.OpenAsync<CounterView>(playAnimation: false);
        await panels.HideAsync(window, false); Assert.False(window.IsVisible);
        Assert.Same(window, await panels.OpenAsync<CounterView>(playAnimation: false));
        await panels.HideAsync(window, false); await panels.ShowAsync(window, false); await panels.CloseAsync(window, false);
        Assert.Equal(3, window.Entered); Assert.Equal(3, window.Exited); Assert.Equal(NodeState.Disposed, window.State);
        var stack = await Attach<StackContainerNode>(ui.Root); var a = await stack.PushAsync<CounterView>(playAnimation: false);
        await stack.PushAsync<CounterView>(playAnimation: false); await stack.PushAsync<CounterView>(playAnimation: false);
        await stack.PopToAsync(a, false); await stack.PopAsync(false); Assert.Empty(stack.History);
        var tabs = await Attach<TabContainerNode>(ui.Root); await tabs.BindAsync("A", new CounterView()); await tabs.BindAsync("B", new UINode());
        await tabs.SwitchAsync<CounterView>(false); await tabs.SwitchAsync("B", false); Assert.Equal("B", tabs.CurrentName);
        var modals = await Attach<ModalContainerNode>(ui.Root); var modal = await modals.OpenAsync<CounterView>(playAnimation: false);
        await modals.CloseAsync(modal, false); Assert.Null(ui.InputOwner); Assert.Empty(((TestHost)ui.Log).Errors);
    }

    [Fact] public async Task FalseIsPerOperationAndDoesNotModifySharedConfiguration()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>();
        var recipe = new Recipe(); var selector = Transitions.Select(_ => recipe); stack.ChildTransition = selector;
        await stack.PushAsync<CounterView>(playAnimation: false); Assert.Empty(recipe.Created);
        await stack.PushAsync<CounterView>(); Assert.Equal(2, recipe.Created.Count); Assert.Same(selector, stack.ChildTransition);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task FalseStillAwaitsLifecycleAndPropagatesCancellationOrFailure(bool cancel)
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); var gate = new TaskCompletionSource<bool>();
        var node = new CounterView(); node.AddLifecycle(new Preparation { Gate = gate }); ui.Register(() => node);
        using var ct = new CancellationTokenSource(); var pending = panels.OpenAsync<CounterView>(playAnimation: false, cancellationToken: ct.Token);
        Assert.False(pending.IsCompleted); Assert.False(node.IsVisible);
        if (cancel) { ct.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); }
        else { var original = new IOException("preparation"); gate.SetException(original); Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => pending)); }
        Assert.Empty(panels.Children); Assert.Equal(NodeState.Disposed, node.State); Assert.True(panels.IsInteractive);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DialogModeCoversOpeningAndClosingBackdropAndNodes(bool cancel)
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory);
        ui.Register<BoolDialog>(); ui.DefaultTransition = Transitions.Select(_ => throw new IOException("Must not select"));
        using var ct = new CancellationTokenSource(); var pending = modals.ShowDialogAsync<BoolDialog, bool>(playAnimation: false, cancellationToken: ct.Token);
        if (cancel) { ct.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); }
        else { ((BoolDialog)modals.Current!).Complete(false); Assert.False(await pending); }
        Assert.Equal(new[] { false, false }, Assert.Single(factory.Created).Modes); Assert.Null(modals.CurrentBackdrop);
        Assert.Null(ui.InputOwner); Assert.Equal(1, factory.Created[0].Disposals);
    }
}
