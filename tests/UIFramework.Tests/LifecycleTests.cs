using UIFramework;
using Xunit;
namespace UIFramework.Tests;

public sealed class RecordingLifecycle : NodeLifecycle
{
    public readonly List<string> Events = new();
    public Action<LifecycleContext>? OnEntered, OnExited;
    public Action<StateChangeContext>? OnActive;
    public Action<StateChangeContext>? OnInactive;
    public Exception? InitializeFailure, DisposeFailure;
    public override Task InitializeAsync(IUINode node, CancellationToken token)
    { Events.Add("initialize"); if (InitializeFailure != null) throw InitializeFailure; return Task.CompletedTask; }
    public override Task EnteringAsync(LifecycleContext c, CancellationToken t) { Events.Add("entering:" + c.Reason); return Task.CompletedTask; }
    public override void Entered(LifecycleContext c) { Events.Add("entered:" + c.Reason); OnEntered?.Invoke(c); }
    public override Task ExitingAsync(LifecycleContext c, CancellationToken t) { Events.Add("exiting:" + c.Reason); return Task.CompletedTask; }
    public override void Exited(LifecycleContext c) { Events.Add("exited:" + c.Reason); OnExited?.Invoke(c); }
    public override void Activated(StateChangeContext c) { Events.Add("active"); OnActive?.Invoke(c); }
    public override void Deactivated(StateChangeContext c) { Events.Add("inactive"); OnInactive?.Invoke(c); }
    public override void VisibilityChanged(StateChangeContext c) => Events.Add("visible:" + c.Current);
    public override Task DisposeAsync(IUINode node)
    { Events.Add("dispose"); if (DisposeFailure != null) throw DisposeFailure; return Task.CompletedTask; }
}

public class LifecycleTests
{
    private static UIContext Create() { var host = new TestHost(); return new UIContext(host, host, host); }
    private static async Task<T> Attach<T>(UIContext ui) where T : UINode, new()
    { var n = new T(); await n.AttachAsync(ui.Root, typeof(T).Name); return n; }

    [Fact] public async Task CompletedStageObservesCommittedStackAndUnlockedInput()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui);
        var behavior = new RecordingLifecycle(); var n = new CounterView(); n.AddLifecycle(behavior);
        ui.Register(() => n);
        behavior.OnEntered = c => { Assert.Same(n, stack.Current); Assert.Single(stack.History); Assert.True(n.IsInteractive); Assert.Same(stack, c.Container); Assert.True(c.IsFirstEnter); };
        await stack.PushAsync<CounterView>();
        Assert.Equal(new[] { "initialize", "entering:Push", "visible:True", "active", "entered:Push" }, behavior.Events);
    }
    [Fact] public async Task CompletedFailureKeepsCommittedNodeAndOriginalException()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui);
        var error = new IOException("did enter"); var b = new RecordingLifecycle { OnEntered = _ => throw error };
        var n = new CounterView(); n.AddLifecycle(b); ui.Register(() => n);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<CounterView>()));
        Assert.Same(n, stack.Current); Assert.Equal(NodeState.Ready, n.State); Assert.True(n.IsInteractive);
        Assert.Equal(1, b.Events.Count(e => e == "entered:Push"));
    }
    [Fact] public async Task TabFailureAfterCommitDoesNotRestorePreviousSelection()
    {
        await using var ui = Create(); var tabs = await Attach<TabContainerNode>(ui);
        var a = new UINode(); var b = new UINode(); var behavior = new RecordingLifecycle(); b.AddLifecycle(behavior);
        await tabs.BindAsync("A", a); await tabs.BindAsync("B", b); await tabs.SwitchAsync("A");
        var error = new IOException("activated"); behavior.OnActive = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => tabs.SwitchAsync("B")));
        Assert.Same(b, tabs.Current); Assert.True(b.IsActive); Assert.False(a.IsVisible);
        Assert.DoesNotContain("entered:Switch", behavior.Events);
    }
    [Fact] public async Task TemporaryPanelLockDoesNotDeactivateUnrelatedInstances()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui);
        var b = new RecordingLifecycle(); var a = new CounterView(); a.AddLifecycle(b);
        var count = 0; ui.Register(() => count++ == 0 ? a : new CounterView(), windowMode: WindowInstanceMode.Multiple);
        await panels.OpenAsync<CounterView>(); b.Events.Clear(); var second = await panels.OpenAsync<CounterView>();
        await panels.CloseAsync(second); Assert.Empty(b.Events); Assert.True(a.IsActive);
    }
    [Fact] public async Task ModalCoverOnlyChangesActivationAndRestoresAfterFocus()
    {
        await using var ui = Create(); var host = (TestHost)ui.Host;
        var panels = await Attach<PanelContainerNode>(ui); var modals = await Attach<ModalContainerNode>(ui);
        var b = new RecordingLifecycle(); var page = new CounterView(); page.AddLifecycle(b); ui.Register(() => page); ui.Register<BoolDialog>(() => new BoolDialog());
        await panels.OpenAsync<CounterView>(); b.Events.Clear(); var focus = new object(); host.Focus = focus;
        var modal = await modals.OpenAsync<BoolDialog>(); Assert.Equal(new[] { "inactive" }, b.Events); Assert.True(page.IsVisible);
        b.OnActive = _ => { Assert.Null(ui.InputOwner); Assert.Same(focus, host.Focus); };
        await modals.CloseAsync(modal); Assert.Equal(new[] { "inactive", "active" }, b.Events);
    }
    [Fact] public async Task AncestorHidingReachesSelectedChildWithoutEnteringItAgain()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui); ui.Register<CounterView>(() => new CounterView()); ui.Register<ArgumentView>(() => new ArgumentView());
        var parent = await stack.PushAsync<CounterView>(); var tabs = new TabContainerNode(); await tabs.AttachAsync(parent, "Tabs");
        var b = new RecordingLifecycle(); var selected = new UINode(); selected.AddLifecycle(b);
        await tabs.BindAsync("Selected", selected); await tabs.BindAsync("Other", new UINode()); await tabs.SwitchAsync("Selected"); b.Events.Clear();
        await stack.PushAsync<ArgumentView>("details"); await stack.PopAsync();
        Assert.Equal(new[] { "inactive", "visible:False", "visible:True", "active" }, b.Events);
        Assert.True(selected.IsActive); Assert.False(tabs.Items["Other"].IsActive);
    }
    [Fact] public async Task RemovedHandlerStillReceivesOwnedCleanupOnce()
    {
        var ui = Create(); var tabs = await Attach<TabContainerNode>(ui); var b = new RecordingLifecycle(); var n = new UINode(); n.AddLifecycle(b);
        await tabs.BindAsync("A", n); n.RemoveLifecycle(b); await tabs.SwitchAsync("A"); await ui.DisposeAsync(); await n.DisposeAsync();
        Assert.Equal(new[] { "initialize", "dispose" }, b.Events);
    }
    [Fact] public async Task PartialHandlerInitializationCleansStartedHandlersAndPreservesBothFailures()
    {
        await using var ui = Create(); var tabs = await Attach<TabContainerNode>(ui); var first = new RecordingLifecycle();
        var primary = new IOException("init"); var secondary = new Exception("cleanup");
        var second = new RecordingLifecycle { InitializeFailure = primary, DisposeFailure = secondary };
        var never = new RecordingLifecycle(); var n = new UINode(); n.AddLifecycle(first); n.AddLifecycle(second); n.AddLifecycle(never);
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => tabs.BindAsync("A", n)));
        Assert.Contains(secondary, UIFailure.GetSecondaryExceptions(primary)); Assert.Contains("dispose", first.Events); Assert.Empty(never.Events); Assert.Empty(tabs.Items);
    }
    [Fact] public async Task NotificationMutationIsRejectedWithoutDeadlockOrRollback()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui);
        var b = new RecordingLifecycle(); var n = new CounterView(); n.AddLifecycle(b); ui.Register(() => n);
        b.OnEntered = _ => n.RemoveLifecycle(b);
        await Assert.ThrowsAsync<InvalidOperationException>(() => panels.OpenAsync<CounterView>());
        Assert.Equal(NodeState.Ready, n.State); Assert.Contains(n, panels.Children);
    }
    [Fact] public async Task CloseNotificationFailureStillDisposesClosedNode()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui);
        var b = new RecordingLifecycle(); var n = new CounterView(); n.AddLifecycle(b); ui.Register(() => n);
        await panels.OpenAsync<CounterView>(); var error = new IOException("exited"); b.OnExited = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => panels.CloseAsync(n)));
        Assert.Equal(NodeState.Disposed, n.State); Assert.Empty(panels.Children); Assert.Contains("dispose", b.Events);
    }
    [Fact] public async Task FailedPushDoesNotPublishTransientHidingOfRetainedPage()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui);
        var b = new RecordingLifecycle(); var n = new CounterView(); n.AddLifecycle(b); ui.Register(() => n);
        await stack.PushAsync<CounterView>(); b.Events.Clear();
        ui.Register(() => new ArgumentView { Transition = new FailureTransition(new IOException("transition")) });
        await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<ArgumentView>("failed"));
        Assert.Equal(new[] { "exiting:Covered" }, b.Events); Assert.True(n.IsActive); Assert.Same(n, stack.Current);
    }
    [Fact] public async Task ForcedModalDisposalRestoresActivationAndFocusWithoutNormalExit()
    {
        await using var ui = Create(); var panels = await Attach<PanelContainerNode>(ui); var modals = await Attach<ModalContainerNode>(ui);
        ui.Register<CounterView>(() => new CounterView()); var page = await panels.OpenAsync<CounterView>(); var b = new RecordingLifecycle();
        var modal = new BoolDialog(); modal.AddLifecycle(b); ui.Register(() => modal);
        var host = (TestHost)ui.Host; var focus = new object(); host.Focus = focus;
        await modals.OpenAsync<BoolDialog>(); await modal.DisposeAsync();
        Assert.Null(ui.InputOwner); Assert.True(page.IsActive); Assert.Same(focus, host.Focus);
        Assert.DoesNotContain(b.Events, e => e.StartsWith("exiting") || e.StartsWith("exited")); Assert.Equal("dispose", b.Events.Last());
    }
    [Fact] public async Task ForcedDisposalContinuesOtherNodesNotificationsAndAllCleanupAfterFailure()
    {
        var ui = Create(); var panels = await Attach<PanelContainerNode>(ui);
        var a = new CounterView(); var b = new CounterView(); var first = new RecordingLifecycle(); var second = new RecordingLifecycle();
        a.AddLifecycle(first); b.AddLifecycle(second); var i = 0; ui.Register(() => i++ == 0 ? a : b, windowMode: WindowInstanceMode.Multiple);
        await panels.OpenAsync<CounterView>(); await panels.OpenAsync<CounterView>();
        var original = new IOException("deactivate"); second.OnInactive = _ => throw original;
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(async () => await ui.DisposeAsync()));
        Assert.Contains("inactive", first.Events); Assert.Equal("dispose", first.Events.Last()); Assert.Equal("dispose", second.Events.Last()); Assert.Empty(ui.Root.Children);
    }
    [Fact] public async Task StatePropagationLosesChildrenFirstAndGainsParentsFirst()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui); var order = new List<string>();
        var parent = new CounterView(); var pb = new RecordingLifecycle(); parent.AddLifecycle(pb); ui.Register(() => parent); ui.Register<ArgumentView>(() => new ArgumentView());
        await stack.PushAsync<CounterView>(); var tabs = new TabContainerNode(); await tabs.AttachAsync(parent, "Tabs");
        var child = new UINode(); var cb = new RecordingLifecycle(); child.AddLifecycle(cb); await tabs.BindAsync("Child", child); await tabs.SwitchAsync("Child");
        pb.OnInactive = _ => order.Add("parent-"); cb.OnInactive = _ => order.Add("child-");
        pb.OnActive = _ => order.Add("parent+"); cb.OnActive = _ => order.Add("child+");
        await stack.PushAsync<ArgumentView>("details"); await stack.PopAsync();
        Assert.Equal(new[] { "child-", "parent-", "parent+", "child+" }, order);
    }
    [Fact] public async Task FirstEnterIsDistinctFromTabReshowAndStackReturn()
    {
        await using var ui = Create(); var tabs = await Attach<TabContainerNode>(ui); var n = new UINode(); var b = new RecordingLifecycle(); n.AddLifecycle(b);
        var seen = new List<bool>(); b.OnEntered = c => seen.Add(c.IsFirstEnter);
        await tabs.BindAsync("A", n); await tabs.BindAsync("B", new UINode()); await tabs.SwitchAsync("A"); await tabs.SwitchAsync("B"); await tabs.SwitchAsync("A");
        Assert.Equal(new[] { true, false }, seen); Assert.Equal(1, b.Events.Count(e => e == "initialize"));
    }

    private sealed class GateTransition : IUITransition, IUITransitionRun
    {
        public IUITransitionRun Create(TransitionContext context) => this; public void Restore() { }
        public readonly TaskCompletionSource<bool> Gate = new();
        public Task PlayAsync(CancellationToken token) => Gate.Task.WaitAsync(token);
    }
    [Fact] public async Task ParallelContainerCommitDoesNotPublishOtherPendingTransitions()
    {
        await using var ui = Create(); var first = await Attach<PanelContainerNode>(ui); var second = new PanelContainerNode(); await second.AttachAsync(ui.Root, "Other");
        var gate = new GateTransition(); var behavior = new RecordingLifecycle(); var n = new CounterView { Transition = gate }; n.AddLifecycle(behavior);
        ui.Register(() => n); ui.Register<ArgumentView>(() => new ArgumentView());
        var pending = first.OpenAsync<CounterView>(); Assert.False(n.IsActive); Assert.True(n.IsVisible);
        await second.OpenAsync<ArgumentView>("other"); Assert.Equal(new[] { "initialize", "entering:Open" }, behavior.Events);
        gate.Gate.SetResult(true); await pending; Assert.True(n.IsActive); Assert.Equal("entered:Open", behavior.Events.Last());
    }

    private sealed class RestoringTransition : IUITransition, IUITransitionRun
    {
        public bool FadedOut, Restored;
        public Exception? RestoreFailure;
        public Exception? PlayFailure;
        private bool _entering;
        public Task PlayAsync(CancellationToken token)
        { FadedOut = !_entering; if (PlayFailure != null) throw PlayFailure; return Task.CompletedTask; }
        public IUITransitionRun Create(TransitionContext context) { _entering = context.Entering; return this; }
        public void Restore()
        { Restored = true; FadedOut = false; if (RestoreFailure != null) throw RestoreFailure; }
    }
    [Fact] public async Task RollbackRestoresPresentationAndExposesRestoreFailureAsSecondary()
    {
        await using var ui = Create(); var stack = await Attach<StackContainerNode>(ui); var restore = new RestoringTransition();
        var page = new CounterView { Transition = restore }; ui.Register(() => page); await stack.PushAsync<CounterView>();
        var primary = new IOException("next enter"); var secondary = new Exception("restore"); restore.RestoreFailure = secondary;
        ui.Register(() => new ArgumentView { Transition = new FailureTransition(primary) });
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => stack.PushAsync<ArgumentView>("next")));
        Assert.True(restore.Restored); Assert.False(restore.FadedOut); Assert.Contains(secondary, UIFailure.GetSecondaryExceptions(primary));
        Assert.Same(page, stack.Current); Assert.True(page.IsActive);
    }

    [Fact] public async Task FailedTargetRestoreStillAttemptsSourceRestore()
    {
        await using var ui = Create(); var tabs = await Attach<TabContainerNode>(ui);
        var sourceTransition = new RestoringTransition(); var targetTransition = new RestoringTransition { RestoreFailure = new Exception("target restore") };
        var source = new CounterView { Transition = sourceTransition }; var primary = new IOException("enter target");
        targetTransition.PlayFailure = primary;
        var target = new CounterView { Transition = targetTransition };
        await tabs.BindAsync("Source", source); await tabs.BindAsync("Target", target); await tabs.SwitchAsync("Source");
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => tabs.SwitchAsync("Target")));
        Assert.True(sourceTransition.Restored); Assert.True(targetTransition.Restored); Assert.Same(source, tabs.Current); Assert.True(source.IsInteractive);
        Assert.Contains(targetTransition.RestoreFailure, UIFailure.GetSecondaryExceptions(primary));
    }

    [Fact] public async Task ReadOnlyLifecycleCannotCompleteDialogResult()
    {
        await using var ui = Create();
        var modals = await Attach<ModalContainerNode>(ui);
        var dialog = new BoolDialog();
        dialog.AddLifecycle(new RecordingLifecycle { OnActive = _ => dialog.Complete(true) });
        ui.Register(() => dialog);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => modals.OpenAsync<BoolDialog>());
        Assert.Contains("Read-only callbacks", failure.Message);
        Assert.Same(dialog, modals.Current);
        Assert.True(dialog.IsInteractive);
        // The rejected notification must not publish a result; an external event can still answer.
        dialog.Complete(false);
        await modals.CloseAsync(dialog);
    }

    [Fact] public async Task DisposalCancellationSharesTheAlreadyPublishedTask()
    {
        await using var ui = Create();
        var node = new UINode();
        var element = new MemoryElement();
        await node.AttachAsync(ui.Root, "CancellationOwner", element);
        Task? repeated = null;
        using var registration = node.Lifetime.Register(() => repeated = node.DisposeAsync().AsTask());

        var disposal = node.DisposeAsync().AsTask();
        await disposal;
        Assert.Same(disposal, repeated);
        await repeated!;
        Assert.Equal(1, element.Disposals);
        Assert.Equal(NodeState.Disposed, node.State);
        Assert.Empty(ui.Root.Children);
        await node.DisposeAsync();
        Assert.Equal(1, element.Disposals);
    }

    private sealed class CanceledCleanup(Exception failure) : UINode
    {
        protected override Task OnDisposeAsync() => throw failure;
    }
    [Fact] public async Task SharedDisposalPreservesCancellationStatusAndOriginalException()
    {
        await using var ui = Create();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var original = new OperationCanceledException(cancel.Token);
        var node = new CanceledCleanup(original);
        var element = new MemoryElement();
        await node.AttachAsync(ui.Root, "CanceledCleanup", element);
        Task? repeated = null;
        using var registration = node.Lifetime.Register(() => repeated = node.DisposeAsync().AsTask());
        var pending = node.DisposeAsync().AsTask();
        Assert.Same(original, await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending));
        Assert.True(pending.IsCanceled);
        Assert.Same(pending, repeated);
        Assert.Same(original, await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repeated!));
        Assert.Equal(1, element.Disposals);
        Assert.Equal(NodeState.Disposed, node.State);
    }

}
