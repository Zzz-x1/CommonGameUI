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
    private PanelContainerNode _stage = null!;
    private PanelContainerNode _cards = null!;
    private StackContainerNode? _inventoryStack;
    private DemoMenuNode _menu = null!;
    private UINode? _scenario;
    private Control _content = null!;
    private bool _switching;
    private bool _exiting;
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
        _ui.RegisterGeneratedUI();
        _menu = new DemoMenuNode();
        await _menu.AttachToAsync(_ui.Root, "DemoMenu", GetNode<Control>("Frame/Layout/Workspace/Sidebar"));
        _stage = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoStage", _content);
        _cards = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoCards", GetNode<Control>("FloatingMount"));
        Modals = await _ui.Root.CreateContainerAsync<ModalContainerNode>("DemoDialogs", GetNode<Control>("ModalMount"));
        Wire("Tabs", OpenTabsAsync);
        Wire("Inventory", OpenInventoryAsync);
        Wire("Settings", async () => await Modals.OpenSettingsWindowViewAsync());
        Wire("Instances", OpenCardsAsync);
        Wire("Confirm", ConfirmAsync);
        Wire("Cancel", CancelLoadingAsync);
        if (OS.GetCmdlineUserArgs().Contains("--self-test")) await RunSelfTestAsync();
        else if (OS.GetCmdlineUserArgs().Contains("--capture") || OS.GetCmdlineUserArgs().Contains("--capture-settings"))
        {
            await OpenTabsAsync();
            var settingsCapture = OS.GetCmdlineUserArgs().Contains("--capture-settings");
            if (settingsCapture)
            {
                var window = await Modals.OpenSettingsWindowViewAsync();
                await window.Body.PushGraphicsViewAsync();
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await CaptureAsync(settingsCapture ? "demo-settings.png" : "demo.png");
            await _ui.DisposeAsync();
            GetTree().Quit();
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-tab"))
        {
            await OpenTabsAsync();
            await ((EmbeddedTabsDemoView)_scenario!).Tabs.SwitchAsync("MissingTab");
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-duplicate"))
        {
            await OpenTabsAsync();
            var tabs = ((EmbeddedTabsDemoView)_scenario!).Tabs;
            await tabs.BindAsync("Graphics", tabs.GetControl<Control>("Graphics"));
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-load"))
        {
            _ui.Register<FailureView>("res://Scenes/DoesNotExist.tscn");
            await _stage.OpenAsync<FailureView>();
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-lifecycle") || OS.GetCmdlineUserArgs().Contains("--failure-transition"))
        {
            var mode = OS.GetCmdlineUserArgs().Contains("--failure-lifecycle") ? "lifecycle" : "transition";
            _ui.Register(() => new FailureView(mode), "res://Scenes/Settings/Graphics.tscn");
            await _stage.OpenAsync<FailureView>();
        }
    }

    public void Track(Task task) => _tasks.Add(task);
    public void TrackAction(Action action) => Track(InvokeActionAsync(action));
    private static async Task InvokeActionAsync(Action action)
    {
        action(); await Task.CompletedTask;
    }

    public override void _Process(double delta)
    {
        foreach (var task in _tasks.ToArray())
        {
            if (!task.IsCompleted) continue;
            _tasks.Remove(task);
            try { task.GetAwaiter().GetResult(); }
            catch (Exception ex)
            {
                GD.PushError(ex.ToString());
                GetTree().Quit(1);
                throw; // The original task exception remains visible; the failed run is terminated.
            }
        }
    }

    private void Wire(string name, Func<Task> action)
    {
        var button = GetNode<Button>($"Frame/Layout/Workspace/Sidebar/{name}");
        void Handler() => Track(action());
        button.Pressed += Handler; _disconnect.Add(() => button.Pressed -= Handler);
    }

    public void Status(string text) => GetNode<Label>("Frame/Layout/Status").Text = text;

    private async Task ChangeScenarioAsync(Func<Task> action)
    {
        if (_switching) throw new InvalidOperationException("A scenario change is already running.");
        _switching = true;
        try
        {
            if (_scenario != null) { await _stage.CloseAsync(_scenario); _scenario = null; }
            if (_inventoryStack != null) { await _inventoryStack.DisposeAsync(); _inventoryStack = null; }
            _content.GetNode<Control>("Welcome").Hide();
            await action();
        }
        finally { _switching = false; }
    }

    public Task OpenTabsAsync() => ChangeScenarioAsync(async () => _scenario = await _stage.OpenEmbeddedTabsDemoViewAsync());

    public Task OpenInventoryAsync() => ChangeScenarioAsync(async () =>
    {
        var mount = new Control { Name = "InventoryMount", MouseFilter = MouseFilterEnum.Ignore };
        _content.AddChild(mount); mount.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _inventoryStack = new StackContainerNode();
        await _inventoryStack.AttachAsync(_ui.Root, "DemoInventory", new GodotUIElement(mount, owned: true));
        await _inventoryStack.PushInventoryViewAsync();
    });

    public async Task OpenCardsAsync()
    {
        await _cards.OpenCardViewAsync(new CardArgs("CRYSTAL SHARD", new Vector2(520, 180)));
        await _cards.OpenCardViewAsync(new CardArgs("ANCIENT RELIC", new Vector2(890, 340)));
        Status("INSTANCES / Close a card without affecting the other instance.");
    }

    public async Task ConfirmAsync()
    {
        var result = await Modals.ShowConfirmViewAsync();
        Status($"DIALOG / Returned {result}. Focus restored to the previous valid target.");
    }

    public async Task CancelLoadingAsync()
    {
        // Cancellation is a normal, explicitly requested test outcome, not error suppression.
        var host = new GodotUIHost(GetViewport());
        var loading = new DelayedLoader(new GodotSceneLoader(host));
        var ui = GodotUI.Create(this, loader: loading);
        ui.Register<CardView>("res://Scenes/Inventory/Card.tscn");
        var panels = await ui.Root.CreateContainerAsync<PanelContainerNode>("CancellationCase", GetNode<Control>("FloatingMount"));
        var pending = panels.OpenAsync<CardView>(new CardArgs("CANCELED", Vector2.Zero));
        await ui.DisposeAsync();
        await ExpectCancellationAsync(pending);
        Check(loading.InstancesCreated == 0, "Cancellation created a late scene instance.");
        Status("CANCELLATION / Scope disposed. Pending load canceled. No late scene created.");
    }

    private sealed class DelayedLoader(IUIResourceLoader inner) : IUIResourceLoader
    {
        public int InstancesCreated;
        public async Task<IUIElement> LoadAsync(string resource, IUIElement? mount, CancellationToken token)
        {
            await Task.Delay(500, token);
            var display = await inner.LoadAsync(resource, mount, token); InstancesCreated++; return display;
        }
    }

    private static async Task ExpectCancellationAsync(Task pending)
    {
        try { await pending; }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Expected an explicitly canceled operation.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Demo validation failed: {message}");
    }

    private async Task RunSelfTestAsync()
    {
        await RunAdapterSelfTestAsync();
        Check(_ui.Root.Children.Count == 4, "Only project-declared mounts should exist.");
        await OpenTabsAsync();
        var tabs = (EmbeddedTabsDemoView)_scenario!;
        var original = tabs.Tabs.Items["Audio"].Element;
        var childCount = tabs.Display.Get<Control>("TabContent").GetChildCount();
        var slider = tabs.Display.Get<HSlider>("TabContent/Graphics/Value");
        slider.Value = 82;
        await tabs.SelectAsync("Audio"); await tabs.SelectAsync("Graphics");
        Check(slider.Value == 82, "Embedded tab state was lost.");
        Check(ReferenceEquals(original, tabs.Tabs.Items["Audio"].Element), "Tab instance was cloned.");
        Check(tabs.Display.Get<Control>("TabContent").GetChildCount() == childCount, "Extra tab scene created.");
        Check(tabs.Tabs.Items["Graphics"].IsInteractive && !tabs.Tabs.Items["Audio"].IsInteractive, "Tab input state.");
        GD.Print("PASS embedded tabs: names, basic nodes, state, no clones");

        await OpenInventoryAsync();
        var inventory = (InventoryView)_inventoryStack!.Current!;
        await inventory.SelectAsync("Materials");
        await _inventoryStack.PushDetailsViewAsync(); await _inventoryStack.PopAsync();
        Check(ReferenceEquals(inventory, _inventoryStack.Current) && inventory.Tabs.CurrentName == "Materials", "Stack/Tab restoration.");
        GD.Print("PASS stack + embedded tabs: original inventory restored");

        var settingsButton = GetNode<Button>("Frame/Layout/Workspace/Sidebar/Settings");
        settingsButton.GrabFocus();
        var settings = await Modals.OpenSettingsWindowViewAsync();
        Check(!_inventoryStack.IsInteractive, "Modal must block underlying input.");
        Check(!_menu.IsInteractive && settingsButton.FocusMode == FocusModeEnum.None, "Modal must block sidebar keyboard navigation.");
        var firstPage = settings.Body.Current;
        await settings.Body.PushGraphicsViewAsync(); await settings.Body.PopAsync();
        Check(ReferenceEquals(firstPage, settings.Body.Current), "Settings page restoration.");
        Check(settings.Display.Control.GetNode("Backdrop") != null, "Backdrop is missing.");
        await Modals.CloseAsync(settings);
        Check(_inventoryStack.IsInteractive, "Input was not restored.");
        Check(_menu.IsInteractive, "Sidebar input was not restored.");
        Check(GetViewport().GuiGetFocusOwner() == settingsButton, "Focus was not restored.");
        GD.Print("PASS settings modal: single shell, page return, focus");

        await OpenCardsAsync();
        var card1 = _cards.Children[0]; var card2 = _cards.Children[1];
        await _cards.CloseAsync(card1);
        Check(card2.State == NodeState.Ready && card2.IsVisible, "Independent close affected other card.");
        await _cards.CloseAsync(card2);
        GD.Print("PASS independent instances");

        var outerResult = Modals.ShowDialogAsync<ConfirmView, bool>();
        await WaitUntilAsync(() => Modals.Current is ConfirmView view && view.IsInteractive);
        var outer = (ConfirmView)Modals.Current!;
        var innerResult = Modals.ShowDialogAsync<ConfirmView, bool>();
        await WaitUntilAsync(() => Modals.Current is ConfirmView view && view != outer && view.IsInteractive);
        var inner = (ConfirmView)Modals.Current!;
        Check(!outer.IsInteractive, "Nested modal did not block its parent.");
        inner.Complete(false); Check(!await innerResult, "Nested result.");
        Check(outer.IsInteractive, "Outer modal did not regain input.");
        outer.Complete(true); Check(await outerResult, "Outer result.");
        GD.Print("PASS nested modal results");

        await CancelLoadingAsync();
        await _ui.DisposeAsync();
        Check(_ui.Root.Children.Count == 0, "Root cleanup left children.");
        GD.Print("GODOT DEMO SELF-TEST PASSED (6 scenarios)");
        _exiting = true;
        GetTree().Quit(0);
    }

    private async Task RunAdapterSelfTestAsync()
    {
        // Exercise the actual engine API, including borrowed-resource ownership and failures.
        var mount = new Control { Name = "AdapterTestMount" };
        var page = new Control { Name = "Page" };
        AddChild(mount); mount.AddChild(page);
        var ui = GodotUI.Create(this);
        try
        {
            Check(ui.Root.Children.Count == 0, "Adapter setup supplied project nodes.");
            var customLoader = new DelayedLoader(new GodotSceneLoader((GodotUIHost)ui.Host));
            await using (var custom = GodotUI.Create(this, loader: customLoader, logger: (IUILog)ui.Host))
                Check(ReferenceEquals(custom.Loader, customLoader) && ReferenceEquals(custom.Log, ui.Host), "Custom services were replaced.");
            var tabs = await ui.Root.CreateContainerAsync<TabContainerNode>("Tabs", mount);
            await tabs.BindAsync("Page", page);
            await tabs.SwitchAsync("Page");
            Check(tabs.Current!.GetType() == typeof(UINode), "Control-only binding requires a business View.");
            Check(ReferenceEquals(tabs.Current.GetControl(), page), "Embedded Control was cloned.");
            Check(ReferenceEquals(tabs.GetControl<Control>("Page"), page), "Typed Control lookup.");
            await tabs.DisposeAsync();
            Check(GodotObject.IsInstanceValid(mount) && !mount.IsQueuedForDeletion(), "Borrowed mount was freed.");
            Check(GodotObject.IsInstanceValid(page) && !page.IsQueuedForDeletion(), "Borrowed page was freed.");

            var customNode = new DemoMenuNode();
            var attached = await customNode.AttachToAsync(ui.Root, "Custom", mount);
            Check(ReferenceEquals(attached, customNode), "Attachment replaced the supplied node.");
            await customNode.DisposeAsync();
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await ExpectCancellationAsync(ui.Root.CreateContainerAsync<PanelContainerNode>("Canceled", mount, canceled.Token));
            Check(ui.Root.Children.Count == 0, "Canceled helper left an attached container.");

            var original = new InvalidOperationException("Expected adapter attachment initialization failure.");
            var failing = new AdapterFailureNode(original);
            try
            {
                await failing.AttachToAsync(ui.Root, "Failing", mount);
                throw new Exception("Adapter initialization should have failed.");
            }
            catch (InvalidOperationException ex)
            {
                Check(ReferenceEquals(ex, original), "Adapter replaced the original initialization exception.");
                Check(failing.State == NodeState.Disposed && ui.Root.Children.Count == 0, "Failed attachment was not cleaned.");
                Check(!mount.IsQueuedForDeletion(), "Failed attachment destroyed its borrowed Control.");
            }
        }
        finally { await ui.DisposeAsync(); mount.QueueFree(); }
        GD.Print("GODOT ADAPTER SELF-TEST PASSED (setup, custom services, binding, ownership, cancellation, original failure)");
    }

    private sealed class AdapterFailureNode(Exception original) : UINode
    {
        protected override Task OnInitializeAsync(CancellationToken token) => Task.FromException(original);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var attempts = 0;
        while (!condition())
        {
            if (++attempts > 300) throw new TimeoutException("Demo operation did not reach its expected state.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
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
