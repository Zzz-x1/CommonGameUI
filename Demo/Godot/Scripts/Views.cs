using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Generated;

namespace HierarchyDemo;

public abstract class DemoView : UINode
{
    private readonly List<Action> _disconnect = new();
    public GodotUIElement Display => (GodotUIElement)Element!;
    protected void Wire(string path, Func<Task> action)
    {
        var button = this.GetControl<Button>(path);
        void Handler() => DemoApp.Instance.Track(action());
        button.Pressed += Handler;
        _disconnect.Add(() => button.Pressed -= Handler);
    }
    protected override Task OnDisposeAsync()
    {
        foreach (var disconnect in _disconnect) disconnect();
        _disconnect.Clear(); return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Tabs/EmbeddedTabsDemo.tscn")]
public class EmbeddedTabsDemoView : DemoView
{
    public TabContainerNode Tabs { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Tabs", this.GetControl<Control>("TabContent"), token);
        foreach (var name in new[] { "Graphics", "Audio", "Controls" })
        {
            await Tabs.BindAsync(name, this.GetControl<Control>($"TabContent/{name}"), token);
            Wire($"TabButtons/{name}", () => SelectAsync(name));
        }
        await SelectAsync("Graphics", token);
    }
    public async Task SelectAsync(string name, CancellationToken token = default)
    {
        await Tabs.SwitchAsync(name, token);
        foreach (var key in Tabs.Items.Keys)
            this.GetControl<Button>($"TabButtons/{key}").Disabled = key == name;
        DemoApp.Instance.Status($"TAB / {name} selected. Existing basic Node; scene instances reused.");
    }
}

[UIResource("res://Scenes/Settings/SettingsWindow.tscn")]
public class SettingsWindowView : DemoView
{
    public StackContainerNode Body { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Transition = new GodotFadeTransition();
        Body = await this.CreateContainerAsync<StackContainerNode>("Body", this.GetControl<Control>("Center/Window/Margin/BodyMount"), token);
        await Body.PushSettingsViewAsync(token);
    }
}

[UIResource("res://Scenes/Settings/Settings.tscn")]
public class SettingsView : DemoView
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Wire("Graphics", () => ((StackContainerNode)Parent!).PushGraphicsViewAsync());
        Wire("Close", () => DemoApp.Instance.Modals.CloseAsync(Parent!.Parent!));
        return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Settings/Graphics.tscn")]
public class GraphicsView : DemoView
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = new GodotFadeTransition();
        Wire("Back", () => ((StackContainerNode)Parent!).PopAsync());
        return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Inventory/Inventory.tscn")]
public class InventoryView : DemoView
{
    public TabContainerNode Tabs { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Tabs", this.GetControl<Control>("TabContent"), token);
        foreach (var name in new[] { "Equipment", "Materials" })
        {
            await Tabs.BindAsync(name, this.GetControl<Control>($"TabContent/{name}"), token);
            Wire($"TabButtons/{name}", () => SelectAsync(name));
            Wire($"TabContent/{name}/Details", () => ((StackContainerNode)Parent!).PushDetailsViewAsync());
        }
        await SelectAsync("Equipment", token);
    }
    public async Task SelectAsync(string name, CancellationToken token = default)
    {
        await Tabs.SwitchAsync(name, token);
        foreach (var key in Tabs.Items.Keys) this.GetControl<Button>($"TabButtons/{key}").Disabled = key == name;
        DemoApp.Instance.Status($"INVENTORY / {name}. Inspect an item, then return to this same tab.");
    }
}

[UIResource("res://Scenes/Inventory/Details.tscn")]
public class DetailsView : DemoView
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = new GodotFadeTransition();
        Wire("Back", () => ((StackContainerNode)Parent!).PopAsync());
        return Task.CompletedTask;
    }
}

public sealed record CardArgs(string Title, Vector2 Position);

[UIResource("res://Scenes/Inventory/Card.tscn")]
public class CardView : UINode<CardArgs>
{
    private Button? _close;
    private Action? _handler;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        var display = this.GetControl();
        display.Position = Parameters.Position;
        this.GetControl<Label>("Margin/Content/Title").Text = Parameters.Title;
        _close = this.GetControl<Button>("Margin/Content/Close");
        _handler = () => DemoApp.Instance.Track(((PanelContainerNode)Parent!).CloseAsync(this));
        _close.Pressed += _handler;
        return Task.CompletedTask;
    }
    protected override Task OnDisposeAsync()
    {
        if (_close != null && _handler != null) _close.Pressed -= _handler;
        return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Dialogs/Confirm.tscn")]
public class ConfirmView : DialogNode<bool>
{
    private readonly List<Action> _disconnect = new();
    private void Wire(string name, Action action)
    {
        var button = this.GetControl<Button>($"Center/Panel/Margin/Content/{name}");
        void Handler() => DemoApp.Instance.TrackAction(action);
        button.Pressed += Handler; _disconnect.Add(() => button.Pressed -= Handler);
    }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = new GodotFadeTransition();
        Wire("Yes", () => Complete(true));
        Wire("No", () => Complete(false));
        Wire("Nested", () => DemoApp.Instance.Track(DemoApp.Instance.ConfirmAsync()));
        return Task.CompletedTask;
    }
    protected override Task OnDisposeAsync()
    {
        foreach (var disconnect in _disconnect) disconnect(); return Task.CompletedTask;
    }
}
