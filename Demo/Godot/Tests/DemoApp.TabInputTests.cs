using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;
public partial class DemoApp
{
    private sealed class HeldTabTransition : IUITransition, IUITransitionRun
    {
        public readonly TaskCompletionSource<bool> Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IUITransitionRun Create(TransitionContext context) => this;
        public Task PlayAsync(CancellationToken token) => Gate.Task.WaitAsync(token);
        public void Restore() { }
    }

    private void PushMouseClick(Button button)
    {
        var position = button.GetGlobalRect().GetCenter();
        foreach (var pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton
            {
                Position = position, GlobalPosition = position,
                ButtonIndex = MouseButton.Left, Pressed = pressed
            }, true);
    }

    private async Task RunTabInputTestsAsync(Action<bool, string> pass)
    {
        await OpenAsync("Inventory");
        var inventory = (InventoryView)Shell.Pages.Current!;
        var root = inventory.GetRoot();
        var tab = root.GetNode<Button>("Tabs/材料");
        var filter = root.GetNode<LineEdit>("Filter");
        var focusMode = filter.FocusMode;
        tab.GrabFocus();
        var target = inventory.Tabs.GetChild("材料");
        var originalTransition = target.Transition;
        var held = new HeldTabTransition();
        target.Transition = held;
        var switching = inventory.SelectTabAsync("材料");
        pass(Busy && tab.FocusMode == Control.FocusModeEnum.None && filter.FocusMode == Control.FocusModeEnum.None
            && tab.MouseFilter == Control.MouseFilterEnum.Ignore && Shell.GetControl<Button>("Top/Back").Disabled,
            "category transition locks selectors filters and shell navigation");
        int presses = 0;
        void Pressed() => presses++;
        tab.Pressed += Pressed;
        PushMouseClick(tab);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var tasks = _tasks.Count;
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        _Notification((int)NotificationWMCloseRequest);
        pass(presses == 0 && _tasks.Count == tasks && Shell.Pages.Current == inventory,
            "category transition rejects native repeated click and global back input");
        tab.Pressed -= Pressed;
        held.Gate.SetResult(true);
        await switching;
        target.Transition = originalTransition;
        pass(!Busy && inventory.Tabs.CurrentName == "材料" && filter.FocusMode == focusMode
            && GetViewport().GuiGetFocusOwner() == tab && !Shell.GetControl<Button>("Top/Back").Disabled,
            "category completion restores controls and selector focus");

        target = inventory.Tabs.GetChild("装备");
        originalTransition = target.Transition;
        held = new HeldTabTransition();
        target.Transition = held;
        var originalFailure = new IOException("Expected category failure.");
        switching = inventory.SelectTabAsync("装备");
        held.Gate.SetException(originalFailure);
        try
        {
            await switching;
            throw new InvalidOperationException("Expected category failure.");
        }
        catch (IOException failure)
        {
            pass(ReferenceEquals(failure, originalFailure) && !Busy && inventory.Tabs.CurrentName == "材料"
                && filter.FocusMode == focusMode && tab.MouseFilter != Control.MouseFilterEnum.Ignore,
                "category failure propagates original error and restores project input");
        }
        held = new HeldTabTransition();
        target.Transition = held;
        using (var cancel = new CancellationTokenSource())
        {
            switching = RunAsync(inventory, () => inventory.Tabs.SwitchAsync("装备", cancellationToken: cancel.Token));
            cancel.Cancel();
            await ExpectCancellationAsync(switching);
        }
        target.Transition = originalTransition;
        pass(!Busy && inventory.Tabs.CurrentName == "材料" && filter.FocusMode == focusMode,
            "category cancellation restores project input without changing selection");

        await ModalAsync(new ModalArgs("Settings"));
        var settings = Modals.Current!;
        var preferences = (TabContainerNode)settings.GetChild("Preferences");
        target = preferences.GetChild("画面");
        originalTransition = target.Transition;
        held = new HeldTabTransition();
        target.Transition = held;
        var settingsTab = settings.GetControl<Button>("Center/Pages/Settings/Tab1");
        settingsTab.GrabFocus();
        switching = PressAsync(settingsTab);
        pass(Busy && settingsTab.FocusMode == Control.FocusModeEnum.None
            && settings.GetControl<Button>("Center/Close").MouseFilter == Control.MouseFilterEnum.Ignore,
            "settings category locks selectors and modal close together");
        tasks = _tasks.Count;
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        pass(_tasks.Count == tasks && Modals.Current == settings, "settings category blocks cross-layer escape");
        held.Gate.SetResult(true);
        await switching;
        target.Transition = originalTransition;
        pass(!Busy && preferences.CurrentName == "画面" && GetViewport().GuiGetFocusOwner() == settingsTab,
            "settings category restores focused selector");
        await BackAsync();

        await OpenAsync("Events");
        var events = (EventsView)Shell.Pages.Current!;
        target = events.Tabs.Current!;
        originalTransition = target.Transition;
        held = new HeldTabTransition();
        target.Transition = held;
        var fold = events.GetControl<Button>("Fold");
        fold.GrabFocus();
        switching = events.FoldAsync();
        pass(Busy && fold.FocusMode == Control.FocusModeEnum.None
            && events.GetControl<Button>("Tabs/签到").MouseFilter == Control.MouseFilterEnum.Ignore,
            "activity fold locks fold and category selectors");
        held.Gate.SetResult(true);
        await switching;
        target.Transition = originalTransition;
        pass(!Busy && events.Tabs.Current == null && events.GetRoot().IsVisibleInTree()
            && GetViewport().GuiGetFocusOwner() == fold, "activity fold restores input and retains its page");
        await events.FoldAsync();
        pass(events.Tabs.CurrentName == "庆典" && !Busy, "activity fold can restore retained embedded content");
        await OpenAsync("Home");
        var toTitle = BackAsync();
        while (Modals.Current is not ConfirmView || !Modals.Current.IsInteractive)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ((ConfirmView)Modals.Current).Complete(true);
        await toTitle;
        var login = _login!.GetControl<Button>("LoginCard/Enter");
        presses = 0;
        login.Pressed += Pressed;
        var entering = EnterAsync();
        pass(Busy && login.FocusMode == Control.FocusModeEnum.None && login.MouseFilter == Control.MouseFilterEnum.Ignore,
            "login preparation locks its enter button before awaiting resources");
        PushMouseClick(login);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        pass(presses == 0, "login preparation ignores native repeated enter click");
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SkipCinematic!();
        await entering;
        login.Pressed -= Pressed;
        pass(!Busy && Shell.Home.IsInteractive && Shell.GetControl<Button>("Top/Back").MouseFilter != Control.MouseFilterEnum.Ignore,
            "login scope restores new shell input after lobby entry");

        var mount = new Control();
        AddChild(mount);
        mount.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await using var fixture = EngineUI.UI.Create(this);
        fixture.Register(() => new UINode(), "res://Scenes/Client/Inventory.tscn");
        var panels = await fixture.Root.CreateContainerAsync<PanelContainerNode>("DisposalScope", mount);
        var owner = await panels.OpenAsync<UINode>(playAnimation: false);
        var tabs = await owner.CreateContainerAsync<TabContainerNode>("Tabs", owner.GetControl<Control>("Pages"));
        await tabs.BindAsync("A", owner.GetControl<Control>("Pages/装备"));
        await tabs.BindAsync("B", owner.GetControl<Control>("Pages/材料"));
        await tabs.SwitchAsync("A", playAnimation: false);
        held = new HeldTabTransition();
        tabs.ChildTransition = held;
        switching = RunAsync(owner, () => tabs.SwitchAsync("B"));
        await fixture.DisposeAsync();
        await ExpectCancellationAsync(switching);
        pass(owner.State == NodeState.Disposed && mount.GetChildCount() == 0 && !Busy
            && !Shell.GetControl<Button>("Top/Back").Disabled,
            "scope disposal cancels child operation without restoring destroyed controls or leaving input locked");
        RemoveChild(mount);
        mount.QueueFree();
    }
}
