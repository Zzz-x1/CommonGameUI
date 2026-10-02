using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Generated;

namespace HierarchyDemo;

public partial class DemoApp
{
    private async Task RunDevelopmentModeAsync()
    {
        if (OS.GetCmdlineUserArgs().Contains("--self-test")) await RunSelfTestAsync();
        else if (OS.GetCmdlineUserArgs().Contains("--capture-camp"))
        {
            await OpenJournalAsync(); var journal = (JournalView)_scenario!;
            await journal.ToggleAsync("Tasks"); journal.GetControl<TextEdit>("Pages/Tasks/Note").Text = "Meet the merchant before dawn.";
            await CaptureAsync("journal-tasks.png");
            await journal.ToggleAsync("Tasks"); await CaptureAsync("journal-folded.png");
            var shop = await Modals.OpenShopViewAsync();
            shop.GetControl<LineEdit>("Center/Panel/Margin/Content/Note").Text = "Hold this bow for Lyra.";
            await Modals.OpenShopItemViewAsync(); var purchase = await Modals.OpenPurchaseViewAsync();
            await CaptureAsync("merchant-purchase.png"); await purchase.ConfirmAsync(); await CaptureAsync("merchant-returned.png");
            await _ui.DisposeAsync(); GetTree().Quit();
        }
        else if (OS.GetCmdlineUserArgs().Contains("--capture-partner"))
        {
            await CapturePartnerAsync();
            await _ui.DisposeAsync(); GetTree().Quit();
        }
        else if (OS.GetCmdlineUserArgs().Any(a => new[] { "--capture", "--capture-settings", "--capture-preferences", "--capture-notes", "--capture-dialogs" }.Contains(a)))
        {
            var preferencesCapture = OS.GetCmdlineUserArgs().Contains("--capture-preferences");
            if (preferencesCapture) await OpenPreferencesAsync(); else await OpenInventoryAsync();
            var settingsCapture = OS.GetCmdlineUserArgs().Contains("--capture-settings");
            var notesCapture = OS.GetCmdlineUserArgs().Contains("--capture-notes");
            var dialogsCapture = OS.GetCmdlineUserArgs().Contains("--capture-dialogs");
            if (dialogsCapture)
            {
                await Modals.OpenSettingsWindowViewAsync();
                await Modals.OpenAsync<ConfirmView>();
            }
            if (settingsCapture)
            {
                var window = await Modals.OpenSettingsWindowViewAsync();
                await window.Body.PushGraphicsViewAsync();
            }
            if (notesCapture)
            {
                await ((InventoryWindowView)_scenario!).Inventory.InspectAsync("Oakheart Bow");
                await _inventoryStack!.PushProvenanceViewAsync();
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await CaptureAsync(dialogsCapture ? "demo-dialogs.png" : settingsCapture ? "demo-settings.png" : preferencesCapture ? "demo-preferences.png" : notesCapture ? "demo-notes.png" : "demo.png");
            await _ui.DisposeAsync();
            GetTree().Quit();
        }
    }
    private async Task CapturePartnerAsync()
    {
        await OpenInventoryAsync();
        var inventory = ((InventoryWindowView)_scenario!).Inventory;
        await CaptureAsync("partner-list.png");
        using var detailsResources = await _ui.PreloadAsync<DetailsView>();
        var entering = inventory.InspectAsync("Oakheart Bow");
        while (inventory.Parent!.Children.OfType<DetailsView>().Single().TransitionProgress < .25f && !entering.IsCompleted)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await CaptureAsync("partner-expanding.png");
        await entering;
        await CaptureAsync("partner-details.png");
        var stack = (StackContainerNode)inventory.Parent!;
        var details = (DetailsView)stack.Current!;
        var returning = stack.PopAsync();
        while (details.TransitionProgress < .25f && !returning.IsCompleted)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await CaptureAsync("partner-returning.png");
        await returning;
    }

    private async Task CaptureAsync(string fileName)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        var path = ProjectSettings.GlobalizePath($"res://../../artifacts/{fileName}");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = image.SavePng(path);
        if (result != Error.Ok) throw new IOException($"Cannot save preview: {result}");
        GD.Print($"Preview saved: {path}");
    }

}
