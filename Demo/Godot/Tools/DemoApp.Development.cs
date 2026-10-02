using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;
public partial class DemoApp
{
    private async Task RunDevelopmentAsync()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--self-test"))
            await RunClientTestsAsync();
        else if (args.Contains("--verify-client-save"))
            await VerifyClientSaveAsync();
        else if (args.Contains("--capture-client"))
        {
            var size = args.SingleOrDefault(x => x.StartsWith("--size=")) ?? "--size=1600x900";
            var parts = size[7..].Split('x');
            GetWindow().Borderless = true;
            GetWindow().Size = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await CaptureClientAsync();
        }
    }

    private async Task CaptureAsync(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var size = GetWindow().Size;
        var folder = ProjectSettings.GlobalizePath($"res://../../artifacts/client-{size.X}x{size.Y}");
        Directory.CreateDirectory(folder);
        var image = GetViewport().GetTexture().GetImage();
        var result = image.SavePng(Path.Combine(folder, name + ".png"));
        if (result != Error.Ok)
            throw new IOException($"截图失败：{result}");
        GD.Print($"CAPTURE {name} {image.GetWidth()}x{image.GetHeight()}");
    }

    private async Task CaptureClientAsync()
    {
        await CaptureAsync("login");
        await EnterAsync();
        await CaptureAsync("lobby");
        foreach (var module in new[]
        {
            "Characters",
            "Inventory",
            "Team",
            "Stages",
            "Shop",
            "Tasks",
            "Achievements",
            "Mail",
            "Friends",
            "Events"
        })
        {
            await OpenAsync(module);
            await CaptureAsync(module.ToLowerInvariant());
            await OpenAsync("Home");
        }

        await ModalAsync(new ModalArgs("Profile"));
        await CaptureAsync("profile");
        await BackAsync();
        await ModalAsync(new ModalArgs("Settings"));
        await CaptureAsync("settings");
        await PressAsync(Modals.Current!.GetControl<Button>("Center/Pages/Settings/Tab1"));
        await CaptureAsync("settings-graphics");
        await PressAsync(Modals.Current!.GetControl<Button>("Center/Pages/Settings/Tab2"));
        await CaptureAsync("settings-animation");
        await BackAsync();
        await OpenAsync("Characters");
        var hero = await Shell.Pages.PushAsync<HeroDetailsView>(0);
        await CaptureAsync("hero-details");
        var training = hero.TrainAsync();
        await CaptureDuringAsync("training-mid", training);
        await training;
        await CaptureAsync("training");
        await BackAsync();
        await OpenAsync("Home");
        await OpenAsync("Inventory");
        var inventory = (InventoryView)Shell.Pages.Current!;
        var item = PressAsync(inventory.GetControl().GetNode<Button>("Pages/装备/C0"));
        await CaptureDuringAsync("partner-mid", item);
        await item;
        await CaptureAsync("item-details");
        await ((ItemDetailsView)Shell.Pages.Current!).CompareAsync();
        await ((ItemDetailsView)Shell.Pages.Current!).CompareAsync();
        await ((ClientModalView)Modals.Current!).PickAsync(3);
        await CaptureAsync("compare");
        await CloseComparisonsAsync();
        await OpenAsync("Home");
        await OpenAsync("Shop");
        await ModalAsync(new ModalArgs("Shop", 0));
        var detail = (ClientModalView)Modals.Current!;
        await detail.ConfirmAsync();
        await CaptureAsync("purchase");
        var purchase = ((ClientModalView)Modals.Current!).ConfirmAsync();
        await CaptureDuringAsync("reward-mid", purchase);
        await purchase;
        await CaptureAsync("reward");
        await BackAsync();
        await BackAsync();
        await _ui.DisposeAsync();
        GetTree().Quit();
    }

    private async Task CaptureDuringAsync(string name, Task operation)
    {
        if (name != "partner-mid")
            while (SkipCinematic == null && !operation.IsCompleted)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(name == "partner-mid" ? .24 : .55), SceneTreeTimer.SignalName.Timeout);
        await CaptureAsync(name);
    }

    private async Task PressAsync(Button button)
    {
        int before = _tasks.Count;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Task.WhenAll(_tasks.Skip(before).ToArray());
    }
}
