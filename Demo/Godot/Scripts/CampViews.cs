using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Generated;

namespace HierarchyDemo;

[UIResource("res://Scenes/Camp/Journal.tscn")]
public class JournalView : DemoView
{
    public TabContainerNode Tabs { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Pages", this.GetControl<Control>("Pages"), token);
        foreach (var name in new[] { "Map", "Tasks" })
        {
            await Tabs.BindAsync(name, new UINode(), this.GetControl<Control>($"Pages/{name}"), token);
            Wire($"Buttons/{name}", () => ToggleAsync(name));
        }
    }
    public async Task ToggleAsync(string name)
    {
        if (Tabs.CurrentName == name) await Tabs.HideAsync();
        else await Tabs.SwitchAsync(name);
        foreach (var key in Tabs.Items.Keys) this.GetControl<Button>($"Buttons/{key}").SetPressedNoSignal(Tabs.CurrentName == key);
        this.GetControl<Label>("Prompt").Visible = Tabs.Current == null;
        DemoApp.Instance.Status(Tabs.Current == null ? "Journal / Pages folded away." : $"Journal / {Tabs.CurrentName}");
    }
}

[UIResource("res://Scenes/Camp/Shop.tscn")]
public class ShopView : DemoView
{
    public int Orders { get; private set; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = GodotTransitions.ScaleFade(targetPath: "Center");
        DismissRequested = ct => DemoApp.Instance.Modals.CloseAsync(this, cancellationToken: ct);
        Wire("Center/Panel/Margin/Content/Details", () => DemoApp.Instance.Modals.OpenShopItemViewAsync());
        Wire("Center/Panel/Margin/Content/Close", () => DemoApp.Instance.Modals.CloseAsync(this));
        return Task.CompletedTask;
    }
    public void RecordOrder()
    {
        Orders++;
        this.GetControl<Label>("Center/Panel/Margin/Content/Receipt").Text = $"Expedition list / Oakheart Bow × {Orders}";
        DemoApp.Instance.Status("Merchant / Order recorded. Your merchant notes are kept.");
    }
}

[UIResource("res://Scenes/Camp/ShopItem.tscn")]
public class ShopItemView : DemoView
{
    public ShopView Shop { get; private set; } = null!;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Shop = (ShopView)((ModalContainerNode)Parent!).Current!;
        Transition = GodotTransitions.ScaleFade(targetPath: "Center");
        Wire("Center/Panel/Margin/Content/Buy", () => DemoApp.Instance.Modals.OpenPurchaseViewAsync());
        Wire("Center/Panel/Margin/Content/Back", () => DemoApp.Instance.Modals.CloseAsync(this));
        return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Camp/Purchase.tscn")]
public class PurchaseView : DemoView
{
    public ShopView Shop { get; private set; } = null!;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Shop = ((ShopItemView)((ModalContainerNode)Parent!).Current!).Shop;
        Transition = GodotTransitions.ScaleFade(targetPath: "Center");
        Wire("Center/Panel/Margin/Content/Confirm", ConfirmAsync);
        Wire("Center/Panel/Margin/Content/Cancel", () => DemoApp.Instance.Modals.CloseAsync(this));
        return Task.CompletedTask;
    }
    public async Task ConfirmAsync()
    {
        await DemoApp.Instance.Modals.CloseToAsync(Shop);
        Shop.RecordOrder();
    }
}
