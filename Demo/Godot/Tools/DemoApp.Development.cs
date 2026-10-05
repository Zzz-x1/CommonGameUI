using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using Godot;
using UIFramework;
using UIFramework.Mvvm;
using UIFramework.Godot;

namespace HierarchyDemo;
public partial class DemoApp
{
    private async Task RunDevelopmentAsync()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--lifetime-tests"))
        {
            int checks = 0;
            await RunLifetimeTestsAsync((passed, name) =>
            {
                if (!passed) throw new InvalidOperationException("Lifetime test failed: " + name);
                checks++; GD.Print("PASS " + name);
            });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await _ui.DisposeAsync();
            GD.Print("LIFETIME TESTS PASSED: " + checks);
            GetTree().Quit();
        }
        else if (args.Contains("--self-test"))
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
        await CaptureTimelineAsync("lobby", EnterAsync());
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
            await CaptureTimelineAsync(module.ToLowerInvariant(), OpenAsync(module), frames: 5);
            await CaptureAsync(module.ToLowerInvariant());
            if (module is "Shop" or "Inventory")
            {
                var page = (ClientPage)Shell.Pages.Current!;
                var switching = page.SelectTabAsync("材料");
                await CaptureDuringAsync(module.ToLowerInvariant() + "-category-mid", switching, .14);
                await switching;
            }
            await OpenAsync("Home");
        }

        await OpenAsync("Inventory");
        var fromInventory = OpenAsync("Shop");
        await CaptureDuringAsync("inventory-to-shop-mid", fromInventory, .12);
        await fromInventory;
        var fromShop = OpenAsync("Inventory");
        await CaptureDuringAsync("shop-to-inventory-mid", fromShop, .12);
        await fromShop;
        var pop = Shell.Pages.PopAsync();
        await CaptureDuringAsync("inventory-pop-to-shop-mid", pop, .12);
        await pop;
        await OpenAsync("Home");
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
        var mailOpening = ModalAsync(new ModalArgs("Mail", 0));
        await CaptureDuringAsync("mail-reveal-mid", mailOpening, .16);
        await mailOpening;
        var mailReward = ((ClientModalView)Modals.Current!).ConfirmAsync();
        await CaptureDuringAsync("mail-reward-reveal-mid", mailReward, .2);
        await mailReward;
        await BackAsync();
        await BackAsync();
        await OpenAsync("Characters");
        var heroOpening = Shell.Pages.PushAsync<HeroDetailsView>(0);
        await CaptureDuringAsync("hero-iris-mid", heroOpening, .2);
        var hero = await heroOpening;
        await CaptureAsync("hero-details");
        var training = hero.TrainAsync();
        await CaptureTimelineAsync("training", training);
        await CaptureAsync("training");
        await BackAsync();
        await OpenAsync("Home");
        await OpenAsync("Inventory");
        var inventory = (InventoryView)Shell.Pages.Current!;
        var item = PressAsync(inventory.GetRoot().GetNode<Button>("Pages/装备/C0"));
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
        await ((ShopView)Shell.Pages.Current!).SelectTabAsync("礼包");
        await ModalAsync(new ModalArgs("Shop", 0, Text: "装备与经验手记补给礼包"));
        var detail = (ClientModalView)Modals.Current!;
        var confirming = detail.ConfirmAsync();
        await CaptureDuringAsync("purchase-blinds-mid", confirming, .16);
        await confirming;
        await CaptureAsync("purchase");
        var purchase = PressAsync(Modals.Current!.GetControl<Button>("Center/Confirm"));
        await CaptureTimelineAsync("reward", purchase);
        await CaptureAsync("reward");
        await BackAsync();
        await BackAsync();
        await _ui.DisposeAsync();
        GetTree().Quit();
    }

    private async Task CaptureDuringAsync(string name, Task operation, double time = .24)
    {
        await ToSignal(GetTree().CreateTimer(time), SceneTreeTimer.SignalName.Timeout);
        await CaptureAsync(name);
    }

    private async Task CaptureTimelineAsync(string name, Task operation, int frames = 10)
    {
        while (SkipCinematic == null && !operation.IsCompleted)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var player = ClientView.Descendants(this).OfType<AnimationPlayer>().Single(p => p.IsPlaying() && p.CurrentAnimation == "enter");
        var length = player.GetAnimation("enter").Length;
        for (int i = 0; i < frames; i++)
        {
            while (player.CurrentAnimationPosition < i * length / frames && !operation.IsCompleted)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await CaptureAsync($"{name}-timeline-{i:00}");
        }
        await operation;
    }

    private async Task PressAsync(Button button)
    {
        int before = _tasks.Count;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Task.WhenAll(Task.WhenAll(_tasks.Skip(before).ToArray()), _ui.WhenMvvmIdleAsync());
    }
}
