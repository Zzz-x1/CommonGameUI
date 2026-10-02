using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;

public partial class DemoApp
{
    private async Task RunSourceBranchTestsAsync(Action<bool, string> pass)
    {
        await OpenAsync("Home");
        var entry = OpenAsync("Inventory");
        while (SkipCinematic == null && !entry.IsCompleted)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        pass(SkipCinematic != null && Shell.GetControl().IsVisibleInTree(), "lobby source chooses visible page timeline");
        await entry;
        var right = OpenAsync("Shop");
        await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        var shop = Shell.Pages.Children.OfType<ShopView>().Single();
        pass(SkipCinematic == null && shop.GetControl().Position.X > 0, "inventory to shop chooses right-side slide");
        await right;
        var left = OpenAsync("Inventory");
        await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        var inventory = Shell.Pages.Children.OfType<InventoryView>().Last();
        pass(SkipCinematic == null && inventory.GetControl().Position.X < 0, "shop to inventory chooses left-side slide");
        await left;
        var returning = Shell.Pages.PopAsync();
        await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        pass(SkipCinematic == null, "pop phase uses short return without replaying timeline");
        await returning;
        await OpenAsync("Home");
        await ModalAsync(new ModalArgs("Mail", 0));
        var reward = RewardAsync("邮件表现分支", new RewardEntry("金币", "compass", 300));
        await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        pass(SkipCinematic == null && Modals.Children.OfType<RewardView>().Single().GetControl<Control>("Center").Material is ShaderMaterial,
            "mail source selects reveal for the same reward view");
        await reward;
        await BackAsync();
        await BackAsync();
    }

    private async Task RunRevealTestsAsync(Action<bool, string> pass)
    {
        for (int style = 0; style < 3; style++)
        {
            var mount = new Control();
            var page = new Control();
            var child = new ColorRect();
            AddChild(mount);
            mount.AddChild(page);
            page.AddChild(child);
            mount.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            page.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            using var originalMaterial = new CanvasItemMaterial();
            child.Material = originalMaterial;
            var log = new TestLog();
            await using var fixture = GodotUI.Create(this, logger: log);
            var tabs = await fixture.Root.CreateContainerAsync<TabContainerNode>("Reveal", mount);
            var node = new UINode { Transition = new GildedRevealTransition(style, .15) };
            await tabs.BindAsync("Page", node, page);
            await tabs.SwitchAsync("Page");
            pass(page.Material == null && ReferenceEquals(child.Material, originalMaterial) && tabs.Current == node,
                $"reveal style {style} completes and restores original materials");
            await tabs.HideAsync(playAnimation: false);
            using var cancel = new CancellationTokenSource();
            void Cancel(TransitionState state)
            {
                if (state.Stage == TransitionStage.Playing && state.Progress > .2f)
                    cancel.Cancel();
            }
            node.TransitionChanged += Cancel;
            await ExpectCancellationAsync(tabs.SwitchAsync("Page", cancellationToken: cancel.Token));
            node.TransitionChanged -= Cancel;
            pass(page.Material == null && ReferenceEquals(child.Material, originalMaterial) && tabs.Current == null,
                $"reveal style {style} cancellation restores material and selection");
            var original = new IOException("Expected reveal observer error.");
            void Fail(TransitionState state)
            {
                if (state.Stage == TransitionStage.Playing && state.Progress > .2f)
                    throw original;
            }
            node.TransitionChanged += Fail;
            try
            {
                await tabs.SwitchAsync("Page");
                throw new InvalidOperationException("Expected reveal failure.");
            }
            catch (IOException exception)
            {
                pass(ReferenceEquals(exception, original) && log.Errors.Contains(original)
                    && page.Material == null && ReferenceEquals(child.Material, originalMaterial) && tabs.Current == null,
                    $"reveal style {style} propagates original error and restores materials");
            }
            node.TransitionChanged -= Fail;
            await fixture.DisposeAsync();
            RemoveChild(mount);
            mount.QueueFree();
        }
    }
}
