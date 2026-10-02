using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Generated;

namespace HierarchyDemo;

public partial class DemoApp : Control
{
    public static DemoApp Instance { get; private set; } = null!;
    private readonly List<Task> _tasks = new();
    private readonly List<Action> _disconnect = new();
    private UIContext _ui = null!;
    private Task<UIResourceLease> _backpackResources = null!;
    private PanelContainerNode _stage = null!;
    private PanelContainerNode _cards = null!;
    private StackContainerNode? _inventoryStack;
    private DemoMenuNode _menu = null!;
    private UINode? _scenario;
    private Control _content = null!;
    private bool _switching;
    private bool _exiting;
    public GameUiState GameState { get; } = new();
    public ModalContainerNode Modals { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
        ApplyTheme();
        _content = GetNode<Control>("Frame/Layout/Workspace/Stage/Margin/Content");
        Track(StartAsync());
    }

    private async Task StartAsync()
    {
        _ui = GodotUI.Create(this);
        _ui.DefaultTransition = GodotTransitions.Fade(0.12);
        _ui.RegisterGeneratedUI();
        _backpackResources = _ui.PreloadAsync<InventoryWindowView>();
        _menu = new DemoMenuNode();
        await _menu.AttachToAsync(_ui.Root, "DemoMenu", GetNode<Control>("Frame/Layout/Workspace/Sidebar"));
        _stage = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoStage", _content);
        _cards = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoCards", GetNode<Control>("FloatingMount"));
        Modals = await _ui.Root.CreateContainerAsync<ModalContainerNode>("DemoDialogs", GetNode<Control>("ModalMount"));
        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared);
        Wire("Tabs", OpenPreferencesAsync);
        Wire("Inventory", OpenInventoryAsync);
        Wire("Settings", () => Modals.OpenSettingsWindowViewAsync());
        Wire("Destinations/Shop", () => Modals.OpenShopViewAsync());
        Wire("Destinations/Journal", OpenJournalAsync);
        Wire("Instances", OpenCardsAsync);
        Wire("Confirm", ConfirmAsync);
        Wire("Cancel", OpenHomeAsync);
        await _backpackResources;
        await RunDevelopmentModeAsync();
    }

    public Task OpenHomeAsync() => ChangeScenarioAsync(() =>
    {
        _content.GetNode<Control>("Welcome").Show();
        Status("Camp / Choose your next destination.");
        return Task.CompletedTask;
    });

    public void Track(Task task) => _tasks.Add(task);
    public override void _Process(double delta)
    {
        if (_inventoryStack?.Current is InventoryView inventory) inventory.Tick(delta);
        foreach (var task in _tasks.ToArray())
        {
            if (!task.IsCompleted) continue;
            _tasks.Remove(task);
            task.GetAwaiter().GetResult();
        }
    }

    private void Wire(string name, Func<Task> action)
    {
        var button = GetNode<Button>($"Frame/Layout/Workspace/Sidebar/{name}");
        void Handler() => Track(action());
        button.Pressed += Handler; _disconnect.Add(() => button.Pressed -= Handler);
    }

    public void Status(string text) => GetNode<Label>("Frame/Layout/Status").Text = text;

    private async Task ChangeScenarioAsync(Func<Task> action, UINode? target = null)
    {
        if (_switching) throw new InvalidOperationException("A scenario change is already running.");
        _switching = true;
        try
        {
            if (_scenario != null && !ReferenceEquals(_scenario, target))
            {
                if (_scenario is InventoryWindowView) await _stage.HideAsync(_scenario);
                else await _stage.CloseAsync(_scenario);
                _scenario = null;
            }
            _content.GetNode<Control>("Welcome").Hide();
            await action();
        }
        finally { _switching = false; }
    }

    public Task OpenPreferencesAsync() => ChangeScenarioAsync(async () => _scenario = await _stage.OpenPreferencesViewAsync());
    public Task OpenJournalAsync() => ChangeScenarioAsync(async () => _scenario = await _stage.OpenJournalViewAsync());

    public Task OpenInventoryAsync() => ChangeScenarioAsync(async () =>
    {
        var resources = await _backpackResources;
        var window = await _stage.OpenInventoryWindowViewAsync();
        resources.Dispose(); // The opened window now owns its independent resource hold.
        _scenario = window;
        _inventoryStack = window.Body;
        Status($"Backpack / {window.Inventory.Tabs.CurrentName} / Pick up where you left off.");
    }, _stage.TryGet<InventoryWindowView>(out var existing) ? existing : null);

    public async Task OpenCardsAsync()
    {
        await _cards.OpenCardViewAsync(new CardArgs("CRYSTAL SHARD", new Vector2(520, 180)));
        await _cards.OpenCardViewAsync(new CardArgs("ANCIENT RELIC", new Vector2(890, 340)));
        Status("Collection / Compare relics side by side.");
    }

    public async Task ConfirmAsync()
    {
        var result = await Modals.ShowConfirmViewAsync();
        if (result && Modals.Current == null) await OpenHomeAsync();
        Status(result ? "Return to camp confirmed." : "Return canceled. Stay on this screen.");
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest || _exiting) return;
        _exiting = true;
        Track(ShutdownAsync());
    }
    private async Task ShutdownAsync()
    {
        await _ui.DisposeAsync(); GetTree().Quit();
    }
    public override void _ExitTree()
    {
        foreach (var disconnect in _disconnect) disconnect();
        _disconnect.Clear();
    }

    private void ApplyTheme()
    {
        Theme = GD.Load<Theme>("res://Themes/Adventure.tres");
        GetTree().AutoAcceptQuit = false;
    }
}
