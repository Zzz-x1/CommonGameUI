using Godot;
using System;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Godot;

public partial class TutorialSmoke : global::Godot.Node
{
    private Task _run = null!;
    private int _checks;
    public override void _Ready() => _run = VerifyAsync();
    public override void _Process(double delta)
    {
        if (!_run.IsCompleted) return;
        SetProcess(false);
        try { _run.GetAwaiter().GetResult(); GD.Print($"TUTORIAL CHECKS PASSED: {_checks}"); GetTree().Quit(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); _checks++; }
    private async Task WaitAsync(Func<bool> condition)
    {
        for (int frame = 0; frame < 180 && !condition(); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(condition(), "Tutorial operation did not finish.");
    }
    private async Task VerifyAsync()
    {
        var host = GD.Load<PackedScene>("res://Main.tscn").Instantiate<TutorialHost>();
        AddChild(host);
        await host.Initialization;
        Check(host.Pages.Current is MenuView, "Menu must be the first page.");
        host.UI.DefaultTransition = GodotTransitions.Fade(.025);
        var menuButton = ((MenuView)host.Pages.Current!).GetControl<Button>("Content/Settings");
        menuButton.GrabFocus();
        menuButton.EmitSignal(Button.SignalName.Pressed);
        await WaitAsync(() => host.Dialogs.Current is SettingsView { IsInteractive: true });
        var settings = (SettingsView)host.Dialogs.Current!;
        Check(settings.Selected == "Audio", "Audio must be selected initially.");
        var volume = settings.GetControl<HSlider>("Content/Body/Audio/Volume");
        volume.Value = 23;
        await settings.SwitchAsync("Graphics");
        Check(settings.Selected == "Graphics", "Graphics switch failed.");
        await settings.SwitchAsync("Audio");
        Check(volume.Value == 23, "Tab switching must retain the same controls.");
        settings.GetControl<Button>("Content/Close").EmitSignal(Button.SignalName.Pressed);
        await WaitAsync(() => settings.State == NodeState.Disposed && host.Dialogs.Current == null && host.Pages.IsInteractive);
        Check(settings.State == NodeState.Disposed, "Close must dispose settings.");
        Check(GetViewport().GuiGetFocusOwner() == menuButton, "Close must restore menu keyboard focus.");
        var cancel = host.AskQuitAsync();
        await WaitAsync(() => host.Dialogs.Current is ConfirmView { IsInteractive: true });
        ((ConfirmView)host.Dialogs.Current!).GetControl<Button>("Content/No").EmitSignal(Button.SignalName.Pressed);
        await cancel;
        Check(host.UI.Root.State == NodeState.Ready && host.Pages.Current is MenuView, "Cancel must retain the menu.");
        var answer = host.Dialogs.ShowDialogAsync<ConfirmView, bool>();
        await WaitAsync(() => host.Dialogs.Current is ConfirmView { IsInteractive: true });
        var confirm = (ConfirmView)host.Dialogs.Current!;
        confirm.GetControl<Button>("Content/Yes").EmitSignal(Button.SignalName.Pressed);
        Check(await answer, "Confirm must return true.");
        Check(confirm.State == NodeState.Disposed, "Answer waits for dialog cleanup.");
        await host.ShutdownAsync(false);
        Check(host.UI.Root.State == NodeState.Disposed, "Shutdown must dispose Context.");
        host.QueueFree();
    }
}
