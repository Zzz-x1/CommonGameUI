using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class ContainerReturnTests
{
    private static (UIContext UI, TestHost Host) Create()
    {
        var host = new TestHost(); var ui = new UIContext(host, host, host);
        ui.Register<CounterView>(() => new CounterView(), "modal"); ui.Register<BoolDialog>(() => new BoolDialog());
        return (ui, host);
    }
    private static async Task<T> Attach<T>(UIContext ui) where T : UINode, new()
    { var node = new T(); await node.AttachAsync(ui.Root, typeof(T).Name, new MemoryElement()); return node; }

    private sealed class Backdrop : MemoryElement, IUIBackdrop
    {
        public int Hides;
        public Task ShowAsync(bool playAnimation, CancellationToken token) { Visible = true; return Task.CompletedTask; }
        public Task HideAsync(bool playAnimation, CancellationToken token) { Hides++; Visible = false; return Task.CompletedTask; }
        public void Restore(bool visible) => Visible = visible;
    }
    private sealed class Backdrops : IUIBackdropFactory
    {
        public readonly List<Backdrop> Created = new();
        public Task<IUIBackdrop> CreateAsync(IUIElement? mount, Func<Task> request, CancellationToken token)
        { var backdrop = new Backdrop(); Created.Add(backdrop); return Task.FromResult<IUIBackdrop>(backdrop); }
    }
    private sealed class GatedDispose : NodeLifecycle
    {
        public readonly TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task DisposeAsync(IUINode node) { Started.TrySetResult(true); return Gate.Task; }
    }
    private sealed class ExitGate : IUITransition, IUITransitionRun
    {
        public readonly TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TransitionContext? Context;
        public IUITransitionRun Create(TransitionContext context) { Context = context; return this; }
        public Task PlayAsync(CancellationToken token) { Started.TrySetResult(true); return Task.Delay(Timeout.Infinite, token); }
        public void Restore() { }
    }

    [Theory]
    [InlineData(ModalBackdropMode.Shared)]
    [InlineData(ModalBackdropMode.PerModal)]
    public async Task ModalReturnKeepsTargetWithoutActivatingOrAnimatingIntermediateModal(ModalBackdropMode mode)
    {
        var (ui, host) = Create(); await using var scope = ui;
        var modals = await Attach<ModalContainerNode>(ui); var backdrops = new Backdrops(); modals.ConfigureBackdrop(backdrops, mode);
        var target = await modals.OpenAsync<CounterView>();
        var focus = new object(); host.Focus = focus;
        var middle = await modals.OpenAsync<CounterView>();
        var top = await modals.OpenAsync<CounterView>();
        var middleState = middle.TransitionState; var targetState = target.TransitionState;
        await modals.CloseToAsync(target, playAnimation: false);
        Assert.Same(target, modals.Current); Assert.Same(target, ui.InputOwner); Assert.True(target.IsInteractive);
        Assert.Same(focus, host.Focus); Assert.Equal(NodeState.Disposed, middle.State); Assert.Equal(NodeState.Disposed, top.State);
        Assert.Equal(0, middle.Exited); Assert.Equal(1, top.Exited); Assert.Same(middleState, middle.TransitionState); Assert.Same(targetState, target.TransitionState);
        Assert.Equal(1, middle.Activated); Assert.Equal(2, target.Activated);
        Assert.Same(target, top.TransitionState!.Context.Target); Assert.Equal(LifecycleReason.Return, top.TransitionState.Context.Reason);
        Assert.Equal(TransitionPhase.Exit, top.TransitionState.Context.Phase);
        Assert.True(backdrops.Created[0].Visible); Assert.Equal(0, backdrops.Created[0].Disposals);
        if (mode == ModalBackdropMode.PerModal)
        {
            Assert.All(backdrops.Created.Skip(1), b => { Assert.False(b.Visible); Assert.Equal(1, b.Disposals); });
            Assert.Equal(0, backdrops.Created[1].Hides); Assert.Equal(1, backdrops.Created[2].Hides);
        }
        else Assert.Single(backdrops.Created);
        Assert.Equal(0, host.Resources[0].Disposals); Assert.Equal(0, host.Loaded[0].Disposals);
        Assert.All(host.Loaded.Skip(1), e => Assert.Equal(1, e.Disposals));
    }

    [Fact] public async Task InvalidModalTargetsDoNotChangeHistory()
    {
        var (ui, _) = Create(); await using var scope = ui;
        var modals = await Attach<ModalContainerNode>(ui); var other = await Attach<ModalContainerNode>(ui);
        var target = await modals.OpenAsync<CounterView>(); var top = await modals.OpenAsync<CounterView>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => modals.CloseToAsync(null!));
        await Assert.ThrowsAsync<InvalidOperationException>(() => modals.CloseToAsync(top));
        await Assert.ThrowsAsync<InvalidOperationException>(() => modals.CloseToAsync(other));
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.CloseToAsync(target));
        Assert.Same(top, modals.Current); Assert.Equal(2, modals.Children.Count); Assert.True(top.IsInteractive);
    }

    [Fact] public async Task CancelBeforeModalCommitRestoresOriginalHistoryAndBackdrop()
    {
        var (ui, _) = Create(); await using var scope = ui;
        var modals = await Attach<ModalContainerNode>(ui); var backdrops = new Backdrops(); modals.ConfigureBackdrop(backdrops);
        var target = await modals.OpenAsync<CounterView>(); var middle = await modals.OpenAsync<CounterView>(); var top = await modals.OpenAsync<CounterView>();
        var gate = new ExitGate(); top.Transition = gate; using var cancellation = new CancellationTokenSource();
        var closing = modals.CloseToAsync(target, cancellationToken: cancellation.Token);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(middle.IsVisible); Assert.True(target.IsVisible); Assert.False(middle.IsActive);
        await Assert.ThrowsAsync<InvalidOperationException>(() => modals.CloseAsync(top));
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing);
        Assert.Same(top, modals.Current); Assert.Same(top, ui.InputOwner); Assert.True(top.IsVisible && top.IsInteractive);
        Assert.Equal(3, modals.Children.Count); Assert.Equal(NodeState.Ready, middle.State); Assert.True(backdrops.Created[0].Visible);
        Assert.True(middle.IsVisible);
        Assert.False(target.IsActive); Assert.False(middle.IsActive); top.Transition = new InstantTransition();
        await modals.CloseToAsync(target); Assert.Same(target, modals.Current);
    }

    [Fact] public async Task CleanupFailuresKeepCommittedTargetAndStillDisposeEveryRemovedModal()
    {
        var (ui, host) = Create(); await using var scope = ui; var modals = await Attach<ModalContainerNode>(ui);
        var target = await modals.OpenAsync<CounterView>(); var middle = await modals.OpenAsync<CounterView>(); var top = await modals.OpenAsync<CounterView>();
        var first = new IOException("top cleanup"); var second = new IOException("middle cleanup");
        top.DisposeFailure = first; middle.DisposeFailure = second;
        Assert.Same(first, await Assert.ThrowsAsync<IOException>(() => modals.CloseToAsync(target)));
        Assert.Contains(second, UIFailure.GetSecondaryExceptions(first)); Assert.Contains(second, host.Errors);
        Assert.Same(target, modals.Current); Assert.True(target.IsInteractive); Assert.Single(modals.Children);
        Assert.Equal(NodeState.Disposed, top.State); Assert.Equal(NodeState.Disposed, middle.State);
        Assert.All(host.Loaded.Skip(1), e => Assert.Equal(1, e.Disposals));
    }

    [Fact] public async Task UncompletedCoveredDialogReturnsCancellationAfterBatchCleanup()
    {
        var (ui, _) = Create(); await using var scope = ui; var modals = await Attach<ModalContainerNode>(ui);
        var target = await modals.OpenAsync<CounterView>();
        var result = modals.ShowDialogAsync<BoolDialog, bool>(); var dialog = modals.Current!;
        await modals.OpenAsync<CounterView>(); await modals.CloseToAsync(target);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result);
        Assert.Equal(NodeState.Disposed, dialog.State); Assert.Same(target, modals.Current);
    }

    [Fact] public async Task CompletedDialogJoinsBatchCloseAndWaitsForCleanup()
    {
        var (ui, _) = Create(); await using var scope = ui; var modals = await Attach<ModalContainerNode>(ui);
        var target = await modals.OpenAsync<CounterView>(); await modals.OpenAsync<CounterView>();
        var disposal = new GatedDispose(); ui.Register(() => { var node = new ResultDialog(); node.AddLifecycle(disposal); return node; });
        var result = modals.ShowDialogAsync<ResultDialog, bool>(); var dialog = (ResultDialog)modals.Current!;
        dialog.Complete(true); var close = modals.CloseToAsync(target, playAnimation: false);
        await disposal.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(result.IsCompleted);
        disposal.Gate.SetResult(true); await close; Assert.True(await result);
        Assert.Same(target, modals.Current); Assert.Equal(NodeState.Disposed, dialog.State);
    }
    public sealed class ResultDialog : DialogNode<bool> { }

    [Fact] public async Task CompletedDialogPropagatesExternalCloseFailureWithoutAutoClosingAgain()
    {
        var (ui, _) = Create(); await using var scope = ui; var modals = await Attach<ModalContainerNode>(ui);
        var target = await modals.OpenAsync<CounterView>(); var result = modals.ShowDialogAsync<BoolDialog, bool>();
        var dialog = (BoolDialog)modals.Current!; var error = new IOException("exit failed");
        dialog.Transition = new FailureTransition(error); dialog.Complete(true);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => modals.CloseToAsync(target)));
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => result));
        Assert.Same(dialog, modals.Current); Assert.True(dialog.IsInteractive);
        dialog.Transition = new InstantTransition(); await modals.CloseToAsync(target);
    }

    [Fact] public async Task DirectExternalCloseCancelsWaitingDialogWithoutDoubleClose()
    {
        var (ui, _) = Create(); await using var scope = ui; var modals = await Attach<ModalContainerNode>(ui);
        var result = modals.ShowDialogAsync<BoolDialog, bool>(); var dialog = modals.Current!;
        await modals.CloseAsync(dialog); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result);
        Assert.Null(modals.Current); Assert.Equal(NodeState.Disposed, dialog.State);
    }

    [Fact] public async Task FailedExternalCloseRestoresUnansweredDialogAndAllowsLaterCompletion()
    {
        var (ui, _) = Create(); await using var scope = ui; var modals = await Attach<ModalContainerNode>(ui);
        var target = await modals.OpenAsync<CounterView>(); var result = modals.ShowDialogAsync<BoolDialog, bool>();
        var dialog = (BoolDialog)modals.Current!; var failure = new IOException("external close failed");
        dialog.Transition = new FailureTransition(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => modals.CloseToAsync(target)));
        Assert.False(result.IsCompleted); Assert.True(dialog.IsInteractive);
        dialog.Transition = new InstantTransition(); dialog.Complete(false);
        Assert.False(await result); Assert.Same(target, modals.Current);
    }

    [Fact] public async Task TargetNotificationFailureStillCleansAllRemovedModals()
    {
        var host = new TestHost(); await using var ui = new UIContext(host, host, host);
        var records = new List<RecordingLifecycle>();
        ui.Register(() => { var node = new CounterView(); var record = new RecordingLifecycle(); records.Add(record); node.AddLifecycle(record); return node; });
        var modals = await Attach<ModalContainerNode>(ui);
        var target = await modals.OpenAsync<CounterView>(); var middle = await modals.OpenAsync<CounterView>(); var top = await modals.OpenAsync<CounterView>();
        records[1].Events.Clear(); var error = new IOException("target activation"); records[0].OnActive = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => modals.CloseToAsync(target)));
        Assert.Same(target, modals.Current); Assert.True(target.IsInteractive); Assert.Single(modals.Children);
        Assert.DoesNotContain("active", records[1].Events); Assert.Equal(NodeState.Disposed, top.State); Assert.Equal(NodeState.Disposed, middle.State);
        records[0].OnActive = null;
    }

    [Fact] public async Task TabHideClearsSelectionRetainsInstancesAndCanReopenByName()
    {
        var (ui, _) = Create(); await using var scope = ui; var tabs = await Attach<TabContainerNode>(ui);
        var first = new CounterView(); var other = new CounterView(); var display = new MemoryElement();
        await tabs.BindAsync("Map", first, display); await tabs.BindAsync("Journal", other);
        await tabs.SwitchAsync("Map"); await tabs.HideAsync(playAnimation: false);
        Assert.Null(tabs.Current); Assert.Null(tabs.CurrentName); Assert.False(first.IsVisible); Assert.False(first.IsActive);
        Assert.Equal(NodeState.Ready, first.State); Assert.Equal(0, display.Disposals); Assert.Equal(2, tabs.Items.Count);
        Assert.Equal(LifecycleReason.Hide, first.TransitionState!.Context.Reason); Assert.Null(first.TransitionState.Context.Target);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tabs.HideAsync());
        await tabs.SwitchAsync("Map"); Assert.Same(first, tabs.Current); Assert.Same(display, first.Element);
        Assert.Equal(1, first.Initialized); Assert.Equal(2, first.Entered); Assert.Equal(1, first.Exited);
    }

    [Fact] public async Task TabHideCancellationAndAnimationFailurePreserveSelection()
    {
        var (ui, _) = Create(); await using var scope = ui; var tabs = await Attach<TabContainerNode>(ui);
        var first = new CounterView(); await tabs.BindAsync("Map", first); await tabs.SwitchAsync("Map");
        var gate = new ExitGate(); first.Transition = gate; using var cancellation = new CancellationTokenSource();
        var hide = tabs.HideAsync(cancellationToken: cancellation.Token); await gate.Started.Task;
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hide);
        Assert.Same(first, tabs.Current); Assert.True(first.IsInteractive);
        var failure = new IOException("hide animation"); first.Transition = new FailureTransition(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => tabs.HideAsync()));
        Assert.Same(first, tabs.Current); Assert.True(first.IsInteractive); first.Transition = new InstantTransition();
        await tabs.HideAsync(); Assert.Null(tabs.Current);
    }

    [Fact] public async Task TabHideNotificationFailureDoesNotRestoreCommittedSelection()
    {
        var (ui, _) = Create(); await using var scope = ui; var tabs = await Attach<TabContainerNode>(ui);
        var page = new CounterView(); var events = new RecordingLifecycle(); page.AddLifecycle(events);
        await tabs.BindAsync("Tasks", page); await tabs.SwitchAsync("Tasks");
        var error = new IOException("hidden notification"); events.OnInactive = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => tabs.HideAsync()));
        Assert.Null(tabs.Current); Assert.False(page.IsVisible); Assert.Equal(NodeState.Ready, page.State);
        events.OnInactive = null; await tabs.SwitchAsync("Tasks"); Assert.Same(page, tabs.Current);
    }
}
