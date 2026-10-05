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
        try
        {
            _run.GetAwaiter().GetResult();
            GD.Print($"TUTORIAL CHECKS PASSED: {_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _checks++;
    }

    private async Task WaitAsync(Func<bool> condition)
    {
        for (var frame = 0; frame < 180 && !condition(); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(condition(), "Tutorial operation did not finish.");
    }

    private async Task VerifyAsync()
    {
        var host = GD.Load<PackedScene>("res://Main.tscn").Instantiate<TutorialHost>();
        AddChild(host);
        await host.Initialization;
        var windows = (PanelContainerNode)host.UI.Root.GetChild("Windows");
        Check(windows.TryGet<HelloWindow>(out var first), "The first window must open at startup.");
        Check(first!.GetControl<Label>("Content/Title").Text == "Hello, UIFramework!", "The window resource must be loaded.");
        Check(first!.GetControl<Button>("Content/Close") != null, "The resource must contain its close button.");
        Check(ReferenceEquals(first, await windows.OpenAsync<HelloWindow>()), "Opening a single window twice must reuse it.");

        first!.GetControl<Button>("Content/Close").EmitSignal(Button.SignalName.Pressed);
        await WaitAsync(() => !windows.TryGet<HelloWindow>(out _));
        host.GetNode<Button>("OpenWindow").EmitSignal(Button.SignalName.Pressed);
        await WaitAsync(() => windows.TryGet<HelloWindow>(out _));
        windows.TryGet<HelloWindow>(out var second);
        Check(!ReferenceEquals(first, second), "Opening after close must create a new instance.");

        await host.UI.DisposeAsync();
        Check(host.UI.Root.State == NodeState.Disposed, "Disposing the Context must close the mounted window.");
        host.QueueFree();
    }
}
