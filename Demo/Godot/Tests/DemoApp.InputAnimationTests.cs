using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;
public partial class DemoApp
{
    private async Task RunInputAnimationTestsAsync(Action<bool, string> pass)
    {
        var mount = new Control();
        AddChild(mount);
        mount.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await using var ui = EngineUI.UI.Create(this);
        ui.Register<CharactersView>("res://Scenes/Client/Characters.tscn");
        var windows = await ui.Root.CreateContainerAsync<PanelContainerNode>("HoverAnimation", mount);
        windows.ChildTransition = new CinematicTransition();
        using var preload = await ui.PreloadAsync<CharactersView>();
        var opening = windows.OpenAsync<CharactersView>();
        var display = windows.Children.Single().GetRoot();
        var card = display.GetNode<Button>("Pages/角色/C0");
        // A mouse-exit feedback can already be running when input becomes locked.
        card.EmitSignal(Control.SignalName.MouseExited);
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var timeline = display.GetNode<AnimationPlayer>("AnimationPlayer");
        timeline.SpeedScale = .0001f;
        await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
        pass(card.Modulate.A == 0, "hover feedback cannot reveal a timeline-hidden card");
        timeline.SpeedScale = 1;
        SkipCinematic!();
        await opening;
        pass(card.Modulate == Colors.White && card.SelfModulate == Colors.White,
            "hover feedback and timeline restore independent color channels");
        var authored = new Color(.8f, .7f, .6f, .25f);
        card.Modulate = authored;
        card.EmitSignal(Control.SignalName.MouseEntered);
        await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
        pass(card.Modulate == authored, "hover feedback preserves authored opacity and tint");
        card.EmitSignal(Control.SignalName.MouseExited);
        await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
        pass(card.Modulate == authored && card.SelfModulate == Colors.White,
            "hover exit restores feedback without overwriting presentation");
        await ui.DisposeAsync();
        RemoveChild(mount);
        mount.QueueFree();

        await OpenAsync("Home");
        var focusMount = new Control();
        var previousFocus = new Button { FocusMode = Control.FocusModeEnum.All, Disabled = true };
        var currentFocus = new Button { FocusMode = Control.FocusModeEnum.All };
        AddChild(focusMount);
        focusMount.AddChild(previousFocus);
        focusMount.AddChild(currentFocus);
        currentFocus.GrabFocus();
        var host = new EngineUI.UIHost(GetViewport());
        host.RestoreFocus(previousFocus);
        pass(GetViewport().GuiGetFocusOwner() == currentFocus, "restore focus does not select a business-disabled button");
        previousFocus.Disabled = false;
        host.RestoreFocus(previousFocus);
        pass(GetViewport().GuiGetFocusOwner() == previousFocus, "restore focus selects a usable original button");
        RemoveChild(focusMount);
        focusMount.QueueFree();
        using var modalPreload = await _ui.PreloadAsync<ClientModalView>();
        var modalOpening = ModalAsync(new ModalArgs("Settings"));
        var tasks = _tasks.Count;
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        _Notification((int)NotificationWMCloseRequest);
        pass(_tasks.Count == tasks, "global input cannot reenter modal opening");
        await modalOpening;
        var modalClosing = Modals.CloseAsync(Modals.Current!);
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        _Notification((int)NotificationWMCloseRequest);
        pass(_tasks.Count == tasks, "global input cannot reenter modal closing");
        await modalClosing;
        var returning = Shell.Pages.PushAsync<CharactersView>();
        _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        _Notification((int)NotificationWMCloseRequest);
        pass(_tasks.Count == tasks, "global input waits for page transition readiness");
        while (SkipCinematic == null)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SkipCinematic!();
        await returning;
        await OpenAsync("Home");
    }
}
