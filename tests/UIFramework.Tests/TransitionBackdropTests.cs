using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class TransitionBackdropTests
{
    private static UIContext Create() { var host = new TestHost(); return new(host, host, host); }
    private static async Task<T> Attach<T>(UINode parent) where T : UINode, new()
    { var node = new T(); await node.AttachAsync(parent, typeof(T).Name, new MemoryElement()); return node; }

    private sealed class Recipe : IUITransition
    {
        public readonly List<Run> Runs = new();
        public Func<TransitionContext, CancellationToken, Task>? Play;
        public Exception? CreateFailure, RestoreFailure;
        public IUITransitionRun Create(TransitionContext context)
        {
            if (CreateFailure != null) throw CreateFailure;
            var run = new Run(this, context); Runs.Add(run); return run;
        }
    }
    private sealed class Run(Recipe owner, TransitionContext context) : IUITransitionRun
    {
        public TransitionContext Context => context;
        public bool Started, Restored;
        public Task PlayAsync(CancellationToken token) { Started = true; return owner.Play?.Invoke(context, token) ?? Task.CompletedTask; }
        public void Restore() { Restored = true; if (owner.RestoreFailure != null) throw owner.RestoreFailure; }
    }
    private sealed class Backdrop : MemoryElement, IUIBackdrop
    {
        public int Shows, Hides, Restores;
        public Exception? ShowFailure, HideFailure, RestoreFailure;
        public Func<CancellationToken, Task>? ShowPlay, HidePlay;
        public Task ShowAsync(bool playAnimation, CancellationToken token) { Shows++; token.ThrowIfCancellationRequested(); Visible = true; if (ShowFailure != null) throw ShowFailure; return ShowPlay?.Invoke(token) ?? Task.CompletedTask; }
        public Task HideAsync(bool playAnimation, CancellationToken token) { Hides++; token.ThrowIfCancellationRequested(); Visible = false; if (HideFailure != null) throw HideFailure; return HidePlay?.Invoke(token) ?? Task.CompletedTask; }
        public void Restore(bool visible) { Restores++; Visible = visible; if (RestoreFailure != null) throw RestoreFailure; }
    }
    private sealed class Factory : IUIBackdropFactory
    {
        public readonly List<Backdrop> Displays = new();
        public Exception? Failure, ShowFailure;
        public Action<Backdrop>? Configure;
        public Task<IUIBackdrop> CreateAsync(IUIElement? mount, Func<Task> request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Failure != null) throw Failure;
            var backdrop = new Backdrop { ShowFailure = ShowFailure }; Configure?.Invoke(backdrop); Displays.Add(backdrop); return Task.FromResult<IUIBackdrop>(backdrop);
        }
    }

    private sealed class EnterPreparation(Task gate) : NodeLifecycle
    {
        public override Task EnteringAsync(LifecycleContext context, CancellationToken token) => gate.WaitAsync(token);
    }

    [Fact] public async Task ModalPreparesBeforeBothAnimationsAndCommitsOnlyAfterBothFinish()
    {
        await using var ui = Create(); var host = (TestHost)ui.Host; var focus = new object(); host.Focus = focus;
        var modals = await Attach<ModalContainerNode>(ui.Root);
        var preparation = new TaskCompletionSource<bool>();
        var window = new TaskCompletionSource<bool>(); var backdrop = new TaskCompletionSource<bool>();
        var windowStarted = new TaskCompletionSource<bool>(); var backdropStarted = new TaskCompletionSource<bool>();
        var view = new CounterView(); view.AddLifecycle(new EnterPreparation(preparation.Task));
        ui.Register(() => view, "modal");
        var recipe = new Recipe { Play = (_, token) => { windowStarted.TrySetResult(true); return window.Task.WaitAsync(token); } };
        modals.ChildTransition = recipe;
        var factory = new Factory { Configure = display => display.ShowPlay = token =>
        { backdropStarted.SetResult(true); return backdrop.Task.WaitAsync(token); } };
        modals.ConfigureBackdrop(factory);
        var opening = modals.OpenAsync<CounterView>();
        Assert.Equal(0, Assert.Single(factory.Displays).Shows); Assert.Empty(recipe.Runs);
        preparation.SetResult(true);
        await Task.WhenAll(windowStarted.Task, backdropStarted.Task).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Null(modals.Current); Assert.Null(ui.InputOwner); Assert.Same(focus, host.Focus); Assert.False(modals.IsInteractive);
        window.SetResult(true); Assert.False(opening.IsCompleted);
        backdrop.SetResult(true); Assert.Same(view, await opening); Assert.Same(view, ui.InputOwner);

        window = new TaskCompletionSource<bool>(); backdrop = new TaskCompletionSource<bool>();
        var display = factory.Displays[0]; display.HidePlay = token => backdrop.Task.WaitAsync(token);
        var closing = modals.CloseAsync(view);
        Assert.True(recipe.Runs.Last().Started); Assert.Equal(1, display.Hides); Assert.False(view.IsInteractive);
        backdrop.SetResult(true); Assert.False(closing.IsCompleted); Assert.Same(view, modals.Current);
        Assert.Equal(0, display.Disposals); Assert.Equal(0, host.Loaded.Single().Disposals);
        window.SetResult(true); await closing;
        Assert.Null(modals.Current); Assert.Null(ui.InputOwner); Assert.Same(focus, host.Focus);
        Assert.Equal(1, display.Disposals); Assert.Equal(1, host.Loaded.Single().Disposals);
    }

    [Theory]
    [InlineData(true, true)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(false, false)]
    public async Task ModalAnimationFailureCancelsPartnerAndWaitsForItsCleanup(bool opening, bool backdropFails)
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root);
        var factory = new Factory(); modals.ConfigureBackdrop(factory); ui.Register<CounterView>(() => new CounterView(), "modal");
        var view = opening ? null : await modals.OpenAsync<CounterView>();
        var failure = new IOException("modal animation"); var failed = new TaskCompletionSource<bool>();
        var canceled = new TaskCompletionSource<bool>(); var cleanup = new TaskCompletionSource<bool>();
        async Task Partner(CancellationToken token)
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { canceled.SetResult(true); await cleanup.Task; throw; }
        }
        Task BackdropPlay(CancellationToken token) => backdropFails ? failed.Task : Partner(token);
        modals.ChildTransition = new Recipe { Play = (_, token) => backdropFails ? Partner(token) : failed.Task };
        if (opening) factory.Configure = display => display.ShowPlay = BackdropPlay;
        else factory.Displays[0].HidePlay = BackdropPlay;
        Task operation = opening ? modals.OpenAsync<CounterView>() : modals.CloseAsync(view!);
        failed.SetException(failure);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(operation.IsCompleted);
        var display = Assert.Single(factory.Displays);
        Assert.Equal(0, display.Disposals); Assert.Equal(0, ((TestHost)ui.Host).Loaded.Single().Disposals);
        cleanup.SetResult(true);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => operation));
        Assert.Single(((TestHost)ui.Log).Errors, error => ReferenceEquals(error, failure));
        if (opening)
        {
            Assert.Empty(modals.Children); Assert.Null(ui.InputOwner); Assert.Equal(1, display.Disposals);
        }
        else
        {
            Assert.Same(view, modals.Current); Assert.Same(view, ui.InputOwner);
            Assert.True(view!.IsInteractive); Assert.True(display.Visible); Assert.True(display.Interactive);
            Assert.Equal(0, display.Disposals);
        }
        Assert.True(modals.IsInteractive);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task ModalCancellationStopsBothAnimationsAndRestoresInput(bool opening)
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root);
        var factory = new Factory(); modals.ConfigureBackdrop(factory); ui.Register<CounterView>(() => new CounterView());
        var view = opening ? null : await modals.OpenAsync<CounterView>();
        int stopped = 0;
        async Task Play(CancellationToken token)
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Interlocked.Increment(ref stopped); }
        }
        modals.ChildTransition = new Recipe { Play = (_, token) => Play(token) };
        if (opening) factory.Configure = display => display.ShowPlay = Play;
        else factory.Displays[0].HidePlay = Play;
        using var cancellation = new CancellationTokenSource();
        Task operation = opening ? modals.OpenAsync<CounterView>(cancellationToken: cancellation.Token)
            : modals.CloseAsync(view!, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, stopped); Assert.True(modals.IsInteractive);
        if (opening) { Assert.Null(modals.Current); Assert.Null(ui.InputOwner); Assert.Equal(1, factory.Displays[0].Disposals); }
        else { Assert.Same(view, modals.Current); Assert.True(view!.IsInteractive); Assert.True(factory.Displays[0].Visible); }
    }

    [Fact] public async Task DisposingModalContainerStopsWindowAndBackdropBeforeReleasingThem()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root);
        int stopped = 0;
        async Task Play(CancellationToken token)
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Interlocked.Increment(ref stopped); }
        }
        var factory = new Factory { Configure = display => display.ShowPlay = Play };
        modals.ConfigureBackdrop(factory); modals.ChildTransition = new Recipe { Play = (_, token) => Play(token) };
        ui.Register<CounterView>(() => new CounterView(), "modal");
        var opening = modals.OpenAsync<CounterView>();
        await modals.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        Assert.Equal(2, stopped); Assert.Equal(1, factory.Displays.Single().Disposals);
        Assert.Equal(1, ((TestHost)ui.Host).Loaded.Single().Disposals);
        Assert.Null(ui.InputOwner); Assert.True(ui.Root.IsInteractive);
    }

    [Fact] public async Task RecipePrecedenceIsNodeThenDirectContainerThenContext()
    {
        await using var ui = Create(); var global = new Recipe(); var local = new Recipe(); var own = new Recipe();
        ui.DefaultTransition = global;
        var panels = await Attach<PanelContainerNode>(ui.Root); panels.ChildTransition = local;
        ui.Register(() => new CounterView { Transition = own }); ui.Register<ArgumentView>(() => new ArgumentView());
        await panels.OpenAsync<CounterView>(); await panels.OpenAsync<ArgumentView>("item");
        Assert.Single(own.Runs); Assert.Single(local.Runs); Assert.Empty(global.Runs);
        var other = await Attach<PanelContainerNode>(ui.Root); await other.OpenAsync<ArgumentView>("other");
        Assert.Single(global.Runs);
    }
    [Fact] public async Task ExplicitInstantDoesNotAnimateAndRepeatedSingleOpenDoesNotReplay()
    {
        await using var ui = Create(); var recipe = new Recipe(); ui.DefaultTransition = recipe;
        var panels = await Attach<PanelContainerNode>(ui.Root);
        ui.Register(() => new CounterView { Transition = new InstantTransition() }); ui.Register<ArgumentView>(() => new ArgumentView());
        await panels.OpenAsync<CounterView>(); var item = await panels.OpenAsync<ArgumentView>("one");
        Assert.Same(item, await panels.OpenAsync<ArgumentView>()); Assert.Single(recipe.Runs);
    }
    [Fact] public async Task StackDirectionAndPopToOnlyAnimateSourceAndTarget()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); var recipe = new Recipe();
        stack.ChildTransition = recipe; ui.Register<CounterView>(() => new CounterView());
        var first = await stack.PushAsync<CounterView>(); await stack.PushAsync<CounterView>(); var top = await stack.PushAsync<CounterView>();
        recipe.Runs.Clear(); await stack.PopToAsync(first);
        Assert.Equal(2, recipe.Runs.Count);
        Assert.All(recipe.Runs, r => { Assert.Equal(TransitionDirection.Backward, r.Context.Direction); Assert.Same(top, r.Context.Source); Assert.Same(first, r.Context.Target); Assert.True(r.Restored); });
        Assert.False(recipe.Runs[0].Context.Entering); Assert.True(recipe.Runs[1].Context.Entering);
    }
    [Fact] public async Task PairedAnimationsStartTogetherAndHistoryCommitsOnlyAfterBothFinish()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView());
        var first = await stack.PushAsync<CounterView>(); var recipe = new Recipe();
        var exit = new TaskCompletionSource<bool>(); var enter = new TaskCompletionSource<bool>();
        recipe.Play = (c, t) => (c.Entering ? enter.Task : exit.Task).WaitAsync(t); stack.ChildTransition = recipe;
        var pending = stack.PushAsync<CounterView>();
        Assert.Equal(2, recipe.Runs.Count); Assert.All(recipe.Runs, r => Assert.True(r.Started)); Assert.Same(first, stack.Current);
        exit.SetResult(true); Assert.False(pending.IsCompleted); Assert.Same(first, stack.Current);
        enter.SetResult(true); await pending; Assert.Equal(2, stack.History.Count);
    }
    [Fact] public async Task FirstAnimationFailureCancelsPartnerAndObservesAllOriginalFailures()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView());
        var first = await stack.PushAsync<CounterView>(); var recipe = new Recipe();
        var primary = new IOException("exit"); var secondary = new InvalidOperationException("enter");
        recipe.Play = (c, _) => Task.FromException(c.Entering ? secondary : primary); stack.ChildTransition = recipe;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<CounterView>()));
        Assert.Contains(secondary, UIFailure.GetSecondaryExceptions(primary)); Assert.All(recipe.Runs, r => Assert.True(r.Restored));
        Assert.Same(first, stack.Current); Assert.True(first.IsInteractive); Assert.Single(stack.Children);
    }
    [Fact] public async Task FailedPartnerCancelsWaitingAnimationAndRestoresState()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView());
        var first = await stack.PushAsync<CounterView>(); var recipe = new Recipe(); var primary = new IOException("enter");
        var canceled = false;
        recipe.Play = async (c, token) =>
        {
            if (c.Entering) throw primary;
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { canceled = true; throw; }
        };
        stack.ChildTransition = recipe;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<CounterView>().WaitAsync(TimeSpan.FromSeconds(2))));
        Assert.True(canceled); Assert.All(recipe.Runs, r => Assert.True(r.Restored)); Assert.Same(first, stack.Current);
    }
    [Fact] public async Task CreatingSecondExecutionFailureRestoresFirstCapturedExecution()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); var source = new Recipe();
        ui.Register(() => new CounterView { Transition = source }); var first = await stack.PushAsync<CounterView>(); source.Runs.Clear();
        var original = new IOException("create target"); var target = new Recipe { CreateFailure = original };
        ui.Register(() => new ArgumentView { Transition = target });
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<ArgumentView>("next")));
        Assert.True(Assert.Single(source.Runs).Restored); Assert.Same(first, stack.Current); Assert.True(first.IsVisible);
    }
    [Fact] public async Task SharedRecipeCreatesIndependentRunsForMultipleWindows()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); var recipe = new Recipe(); panels.ChildTransition = recipe;
        ui.Register<CounterView>(() => new CounterView(), windowMode: WindowInstanceMode.Multiple); var a = await panels.OpenAsync<CounterView>(); var b = await panels.OpenAsync<CounterView>();
        Assert.NotSame(recipe.Runs[0], recipe.Runs[1]); Assert.Same(a, recipe.Runs[0].Context.Node); Assert.Same(b, recipe.Runs[1].Context.Node);
        await panels.HideAsync(a); await panels.ShowAsync(a);
        Assert.Equal(LifecycleReason.Hide, recipe.Runs[2].Context.Reason); Assert.Equal(LifecycleReason.Show, recipe.Runs[3].Context.Reason);
    }
    [Theory] [InlineData(ModalBackdropMode.Shared, 1)] [InlineData(ModalBackdropMode.PerModal, 2)]
    public async Task BackdropOwnershipMatchesConfiguredMode(ModalBackdropMode mode, int count)
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory, mode); ui.Register<CounterView>(() => new CounterView());
        var a = await modals.OpenAsync<CounterView>(); var first = modals.CurrentBackdrop; var b = await modals.OpenAsync<CounterView>();
        Assert.Equal(count, factory.Displays.Count); Assert.Equal(mode == ModalBackdropMode.Shared, ReferenceEquals(first, modals.CurrentBackdrop));
        Assert.All(factory.Displays, display => Assert.Equal(1, display.Shows));
        await modals.CloseAsync(b); Assert.Same(first, modals.CurrentBackdrop); Assert.True(a.IsInteractive);
        Assert.Equal(mode == ModalBackdropMode.Shared ? 0 : 1, factory.Displays.Sum(display => display.Hides));
        Assert.Equal(mode == ModalBackdropMode.Shared ? 0 : 1, factory.Displays.Sum(d => d.Disposals));
        await modals.CloseAsync(a); Assert.Null(modals.CurrentBackdrop); Assert.All(factory.Displays, d => Assert.Equal(1, d.Disposals));
    }
    [Fact] public async Task DismissCompletesDialogWithBusinessResultAndNonDismissibleReturnsFalse()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); modals.ConfigureBackdrop(new Factory()); ui.Register<BoolDialog>(() => new BoolDialog());
        var result = modals.ShowDialogAsync<BoolDialog, bool>(); var dialog = (BoolDialog)modals.Current!;
        Assert.False(await modals.RequestDismissAsync());
        dialog.DismissRequested = _ => { dialog.Complete(false); return Task.CompletedTask; };
        Assert.True(await modals.RequestDismissAsync()); Assert.False(await result); Assert.Null(modals.Current);
    }
    [Fact] public async Task OpeningFailureReleasesNewBackdropAndPreservesOriginal()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var original = new IOException("prepare"); var factory = new Factory();
        modals.ConfigureBackdrop(factory); ui.Register(() => new CounterView { EnterFailure = original });
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => modals.OpenAsync<CounterView>()));
        Assert.Equal(1, Assert.Single(factory.Displays).Disposals); Assert.Empty(modals.Children); Assert.Null(ui.InputOwner);
    }
    [Fact] public async Task FailedNestedOpenKeepsSharedBackdropAndOuterInput()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory);
        ui.Register<CounterView>(() => new CounterView()); var outer = await modals.OpenAsync<CounterView>(); var display = modals.CurrentBackdrop;
        var original = new IOException("nested transition"); ui.Register(() => new ArgumentView { Transition = new FailureTransition(original) });
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => modals.OpenAsync<ArgumentView>("inner")));
        Assert.Same(display, modals.CurrentBackdrop); Assert.Single(factory.Displays); Assert.Equal(0, factory.Displays[0].Disposals); Assert.True(outer.IsInteractive);
    }
    [Fact] public async Task HideFailureRestoresBackdropAndLeavesModalOpen()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory); ui.Register<CounterView>(() => new CounterView());
        var modal = await modals.OpenAsync<CounterView>(); var backdrop = factory.Displays[0]; var primary = new IOException("hide"); backdrop.HideFailure = primary;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => modals.CloseAsync(modal)));
        Assert.Same(modal, modals.Current); Assert.True(modal.IsVisible); Assert.True(backdrop.Visible); Assert.True(backdrop.Interactive);
        backdrop.HideFailure = null;
    }
    [Fact] public async Task ForcedDisposalReleasesAllBackdropDisplaysEvenWhenContentCleanupFails()
    {
        var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory, ModalBackdropMode.PerModal); ui.Register<CounterView>(() => new CounterView());
        var a = await modals.OpenAsync<CounterView>(); var b = await modals.OpenAsync<CounterView>(); b.DisposeFailure = new IOException("content");
        Assert.Same(b.DisposeFailure, await Assert.ThrowsAsync<IOException>(async () => await ui.DisposeAsync()));
        Assert.All(factory.Displays, d => Assert.Equal(1, d.Disposals)); Assert.Null(ui.InputOwner); Assert.Equal(NodeState.Disposed, a.State);
    }
    [Fact] public async Task ContentAndBackdropCleanupFailuresRemainObservable()
    {
        var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory); ui.Register<CounterView>(() => new CounterView());
        var node = await modals.OpenAsync<CounterView>(); var primary = new IOException("content"); var secondary = new InvalidOperationException("backdrop");
        node.DisposeFailure = primary; factory.Displays[0].DisposeFailure = secondary;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => modals.CloseAsync(node)));
        Assert.Contains(secondary, UIFailure.GetSecondaryExceptions(primary)); Assert.Null(modals.Current); Assert.Null(modals.CurrentBackdrop); await ui.DisposeAsync();
    }

    [Fact] public async Task RestoreFailuresDoNotHidePlaybackFailureOrSkipOtherRestores()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui.Root); var recipe = new Recipe(); stack.ChildTransition = recipe; ui.Register<CounterView>(() => new CounterView());
        var page = await stack.PushAsync<CounterView>(); recipe.Runs.Clear();
        var primary = new IOException("animation"); var restore = new InvalidOperationException("restore");
        recipe.Play = (_, _) => Task.FromException(primary); recipe.RestoreFailure = restore;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<CounterView>()));
        Assert.All(recipe.Runs, r => Assert.True(r.Restored)); Assert.Contains(restore, UIFailure.GetSecondaryExceptions(primary)); Assert.Same(page, stack.Current);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task BackdropCreateOrShowFailureDoesNotPublishModal(bool show)
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var original = new IOException("backdrop");
        var factory = new Factory { Failure = show ? null : original, ShowFailure = show ? original : null }; modals.ConfigureBackdrop(factory); ui.Register<CounterView>(() => new CounterView());
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => modals.OpenAsync<CounterView>()));
        Assert.Empty(modals.Children); Assert.Null(modals.Current); Assert.Null(ui.InputOwner);
        Assert.All(factory.Displays, d => Assert.Equal(1, d.Disposals));
    }
    [Fact] public async Task BackdropRestoreFailureIsSecondaryToOriginalHideFailure()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory); ui.Register<CounterView>(() => new CounterView());
        var node = await modals.OpenAsync<CounterView>(); var backdrop = factory.Displays[0];
        var original = new IOException("hide"); var secondary = new InvalidOperationException("restore"); backdrop.HideFailure = original; backdrop.RestoreFailure = secondary;
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => modals.CloseAsync(node)));
        Assert.Contains(secondary, UIFailure.GetSecondaryExceptions(original)); Assert.Same(node, modals.Current);
    }
    [Fact] public async Task DismissFailureAndConcurrentRequestAreObservable()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView()); var node = await modals.OpenAsync<CounterView>();
        var gate = new TaskCompletionSource<bool>(); var original = new IOException("business dismiss");
        node.DismissRequested = async token => { await gate.Task.WaitAsync(token); throw original; };
        var pending = modals.RequestDismissAsync(); await Assert.ThrowsAsync<InvalidOperationException>(() => modals.RequestDismissAsync());
        gate.SetResult(true); Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => pending)); Assert.Same(node, modals.Current);
    }
    [Fact] public async Task ConfigurationCannotChangeWhileOpeningOrWhileModalsExist()
    {
        await using var ui = Create(); var modals = await Attach<ModalContainerNode>(ui.Root); var factory = new Factory(); modals.ConfigureBackdrop(factory);
        var recipe = new Recipe(); var gate = new TaskCompletionSource<bool>(); recipe.Play = (_, t) => gate.Task.WaitAsync(t); modals.ChildTransition = recipe; ui.Register<CounterView>(() => new CounterView());
        var pending = modals.OpenAsync<CounterView>(); Assert.Throws<InvalidOperationException>(() => modals.ConfigureBackdrop(new Factory()));
        gate.SetResult(true); var node = await pending; Assert.Throws<InvalidOperationException>(() => modals.ConfigureBackdrop(new Factory())); await modals.CloseAsync(node);
    }
    [Fact] public async Task ParentInputLocksAlsoGateBackdropRequests()
    {
        await using var ui = Create(); var panel = await Attach<PanelContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView()); var shell = await panel.OpenAsync<CounterView>();
        var modals = await Attach<ModalContainerNode>(shell); var factory = new Factory(); modals.ConfigureBackdrop(factory); await modals.OpenAsync<CounterView>();
        var recipe = new Recipe(); var gate = new TaskCompletionSource<bool>(); recipe.Play = (_, t) => gate.Task.WaitAsync(t); shell.Transition = recipe;
        var pending = panel.HideAsync(shell); Assert.False(factory.Displays[0].Interactive); gate.SetResult(true); await pending;
        Assert.False(factory.Displays[0].Visible); await panel.ShowAsync(shell); Assert.True(factory.Displays[0].Interactive); Assert.True(factory.Displays[0].Visible);
    }
    [Fact] public async Task ModalPreparationBlocksBackgroundInputWithoutPrematureDeactivation()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView()); var page = await panels.OpenAsync<CounterView>();
        var modals = await Attach<ModalContainerNode>(ui.Root); var recipe = new Recipe(); var gate = new TaskCompletionSource<bool>(); recipe.Play = (_, t) => gate.Task.WaitAsync(t); modals.ChildTransition = recipe;
        var pending = modals.OpenAsync<CounterView>(); Assert.True(page.IsActive); Assert.False(page.IsInteractive); Assert.Null(ui.InputOwner);
        gate.SetResult(true); var modal = await pending; Assert.False(page.IsActive); await modals.CloseAsync(modal); Assert.True(page.IsInteractive);
    }
    [Fact] public async Task CancelingModalAnimationReleasesGlobalInputBlock()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView()); var page = await panels.OpenAsync<CounterView>();
        var modals = await Attach<ModalContainerNode>(ui.Root); var recipe = new Recipe(); recipe.Play = (_, t) => Task.Delay(Timeout.Infinite, t); modals.ChildTransition = recipe;
        using var cancel = new CancellationTokenSource(); var pending = modals.OpenAsync<CounterView>(cancellationToken: cancel.Token);
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(page.IsInteractive); Assert.Null(ui.InputOwner); Assert.Empty(modals.Children); Assert.True(Assert.Single(recipe.Runs).Restored);
    }
    [Fact] public async Task DisposingModalContainerDuringAnimationDoesNotLeaveContextInputLocked()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui.Root); ui.Register<CounterView>(() => new CounterView()); var page = await panels.OpenAsync<CounterView>();
        var modals = await Attach<ModalContainerNode>(ui.Root); var recipe = new Recipe(); recipe.Play = (_, t) => Task.Delay(Timeout.Infinite, t); modals.ChildTransition = recipe;
        var pending = modals.OpenAsync<CounterView>(); Assert.False(page.IsInteractive);
        await modals.DisposeAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(page.IsInteractive); Assert.Null(ui.InputOwner); Assert.Equal(NodeState.Disposed, modals.State);
    }
    [Fact] public async Task CompetingModalContainerIsRejectedBeforeChangingExistingInputAndFocus()
    {
        await using var ui = Create(); ui.Register<CounterView>(() => new CounterView()); var first = await Attach<ModalContainerNode>(ui.Root); var second = await Attach<ModalContainerNode>(ui.Root);
        var existing = await first.OpenAsync<CounterView>(); var focus = ((TestHost)ui.Host).Focus;
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.OpenAsync<CounterView>());
        Assert.True(existing.IsInteractive); Assert.Same(focus, ((TestHost)ui.Host).Focus); Assert.Empty(second.Children);
    }
    [Fact] public async Task DisposingTransitionParticipantCancelsPlaybackAndReleasesContainer()
    {
        await using var ui = Create(); var tabs = await Attach<TabContainerNode>(ui.Root);
        var recipe = new Recipe { Play = (_, token) => Task.Delay(Timeout.Infinite, token) };
        var node = new UINode { Transition = recipe }; await tabs.BindAsync("Page", node, new MemoryElement());
        var entering = tabs.SwitchAsync("Page"); Assert.True(Assert.Single(recipe.Runs).Started);
        await node.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entering.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(Assert.Single(recipe.Runs).Restored); Assert.Equal(NodeState.Disposed, node.State); Assert.Null(tabs.Current);
        await tabs.BindAsync("Next", new UINode(), new MemoryElement()); await tabs.SwitchAsync("Next");
        Assert.True(tabs.Current!.IsInteractive);
    }

}
