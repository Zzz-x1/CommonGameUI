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
        _ui.RegisterGeneratedUI();
        _menu = new DemoMenuNode();
        await _menu.AttachToAsync(_ui.Root, "DemoMenu", GetNode<Control>("Frame/Layout/Workspace/Sidebar"));
        _stage = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoStage", _content);
        _cards = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoCards", GetNode<Control>("FloatingMount"));
        Modals = await _ui.Root.CreateContainerAsync<ModalContainerNode>("DemoDialogs", GetNode<Control>("ModalMount"));
        Wire("Tabs", OpenPreferencesAsync);
        Wire("Inventory", OpenInventoryAsync);
        Wire("Settings", () => Modals.OpenSettingsWindowViewAsync());
        Wire("Instances", OpenCardsAsync);
        Wire("Confirm", ConfirmAsync);
        Wire("Cancel", OpenHomeAsync);
        if (OS.GetCmdlineUserArgs().Contains("--self-test")) await RunSelfTestAsync();
        else if (OS.GetCmdlineUserArgs().Contains("--capture") || OS.GetCmdlineUserArgs().Contains("--capture-settings") || OS.GetCmdlineUserArgs().Contains("--capture-preferences"))
        {
            var preferencesCapture = OS.GetCmdlineUserArgs().Contains("--capture-preferences");
            if (preferencesCapture) await OpenPreferencesAsync(); else await OpenInventoryAsync();
            var settingsCapture = OS.GetCmdlineUserArgs().Contains("--capture-settings");
            if (settingsCapture)
            {
                var window = await Modals.OpenSettingsWindowViewAsync();
                await window.Body.PushGraphicsViewAsync();
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await CaptureAsync(settingsCapture ? "demo-settings.png" : preferencesCapture ? "demo-preferences.png" : "demo.png");
            await _ui.DisposeAsync();
            GetTree().Quit();
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-tab"))
        {
            await OpenPreferencesAsync();
            await ((PreferencesView)_scenario!).Tabs.SwitchAsync("MissingTab");
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-duplicate"))
        {
            await OpenPreferencesAsync();
            var tabs = ((PreferencesView)_scenario!).Tabs;
            await tabs.BindAsync("Graphics", tabs.GetControl<Control>("Graphics"));
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-load"))
        {
            _ui.Register<FailureView>("res://Scenes/DoesNotExist.tscn");
            await _stage.OpenAsync<FailureView>();
        }
        else
        {
            var failures = OS.GetCmdlineUserArgs().Where(a => a.StartsWith("--failure-")).ToArray();
            if (failures.Length != 0)
            {
                var mode = failures.Single().Substring("--failure-".Length);
                if (!new[] { "lifecycle", "transition", "entered", "state", "cleanup" }.Contains(mode))
                    throw new ArgumentException($"Unknown failure fixture {mode}.");
                _ui.Register(() => new FailureView(mode), "res://Scenes/Settings/Graphics.tscn");
                var node = await _stage.OpenAsync<FailureView>();
                if (mode == "cleanup") await _stage.CloseAsync(node);
                throw new InvalidOperationException($"Failure fixture {mode} unexpectedly succeeded.");
            }
        }
    }

    public Task OpenHomeAsync() => ChangeScenarioAsync(() =>
    {
        _content.GetNode<Control>("Welcome").Show();
        Status("Camp / Choose your next destination.");
        return Task.CompletedTask;
    });

    public void Track(Task task) => _tasks.Add(task);
    public void TrackAction(Action action)
    {
        try { action(); }
        // Godot signal callbacks must route errors to the same observable frame entry point.
        // Successful synchronous actions need no task or async state machine.
        catch (Exception ex) { Track(Task.FromException(ex)); }
    }

    public override void _Process(double delta)
    {
        if (_inventoryStack?.Current is InventoryView inventory) inventory.Tick(delta);
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

    public Task OpenPreferencesAsync() => ChangeScenarioAsync(async () => _scenario = await _stage.OpenPreferencesViewAsync());

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
        Status("Collection / Compare relics side by side.");
    }

    public async Task ConfirmAsync()
    {
        var result = await Modals.ShowConfirmViewAsync();
        if (result && Modals.Current == null) await OpenHomeAsync();
        Status(result ? "Return to camp confirmed." : "Return canceled. Stay on this screen.");
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
        await OpenPreferencesAsync();
        var tabs = (PreferencesView)_scenario!;
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
        var inspection = inventory.PreviewSeconds;
        await inventory.InspectAsync("Moonstone"); inspection = inventory.PreviewSeconds; inventory.Tick(1);
        Check(inventory.PreviewSeconds == inspection && !inventory.Tabs.Current!.IsActive, "Hidden inventory preview did not pause.");
        var details = (DetailsView)_inventoryStack.Current!;
        Check(details.GetControl<Label>("Title").Text == "MOONSTONE", "Selected item did not reach details.");
        details.GetControl<CheckButton>("Enabled").ButtonPressed = false;
        await _inventoryStack.PopAsync(); inventory.Tick(1);
        Check(!inventory.Favorites.Contains("Moonstone"), "Favorite edit was not retained by the project model.");
        inventory.GetControl<CheckButton>("TabContent/Materials/Enabled").ButtonPressed = true;
        Check(!inventory.GetControl<Button>("TabContent/Materials/Items/Item0").Visible, "Favorites filter did not apply.");
        inventory.GetControl<CheckButton>("TabContent/Materials/Enabled").ButtonPressed = false;
        Check(inventory.EnterCount == 2 && inventory.PreviewSeconds > inspection, "Return lifecycle did not resume inventory.");
        Check(ReferenceEquals(inventory, _inventoryStack.Current) && inventory.Tabs.CurrentName == "Materials", "Stack/Tab restoration.");
        GD.Print("PASS stack + embedded tabs: original inventory restored");

        var settingsButton = GetNode<Button>("Frame/Layout/Workspace/Sidebar/Settings");
        settingsButton.GrabFocus();
        var settings = await Modals.OpenSettingsWindowViewAsync();
        Check(settings.Display.Control.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "Modal did not receive focus after input unlock.");
        Check(!_inventoryStack.IsInteractive, "Modal must block underlying input.");
        inspection = inventory.PreviewSeconds; inventory.Tick(1);
        Check(inventory.PreviewSeconds == inspection && inventory.IsVisible && !inventory.IsActive, "Modal cover must pause a visible inventory.");
        Check(!_menu.IsInteractive && settingsButton.FocusMode == FocusModeEnum.None, "Modal must block sidebar keyboard navigation.");
        var firstPage = settings.Body.Current;
        var graphics = await settings.Body.PushGraphicsViewAsync();
        graphics.GetControl<HSlider>("Value").Value = 74;
        await settings.Body.PopAsync();
        graphics = await settings.Body.PushGraphicsViewAsync();
        Check(graphics.GetControl<HSlider>("Value").Value == 74, "Project preferences did not survive recreated graphics page.");
        await settings.Body.PopAsync();
        Check(ReferenceEquals(firstPage, settings.Body.Current), "Settings page restoration.");
        Check(((SettingsView)firstPage!).EnterCount == 3, "Settings completed return lifecycle.");
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
        var outerFocus = GetViewport().GuiGetFocusOwner();
        var innerResult = Modals.ShowDialogAsync<ConfirmView, bool>();
        await WaitUntilAsync(() => Modals.Current is ConfirmView view && view != outer && view.IsInteractive);
        var inner = (ConfirmView)Modals.Current!;
        Check(!outer.IsInteractive, "Nested modal did not block its parent.");
        inner.Complete(false); Check(!await innerResult, "Nested result.");
        Check(outer.IsInteractive, "Outer modal did not regain input.");
        Check(GetViewport().GuiGetFocusOwner() == outerFocus, "Nested modal focus was not restored.");
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
            await customNode.AttachToAsync(ui.Root, "Custom", mount);
            Check(ReferenceEquals(ui.Root.GetChild("Custom"), customNode), "Attachment replaced the supplied node.");
            await customNode.DisposeAsync();
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await ExpectCancellationAsync(ui.Root.CreateContainerAsync<PanelContainerNode>("Canceled", mount, canceled.Token));
            Check(ui.Root.Children.Count == 0, "Canceled helper left an attached container.");

            var stack = await ui.Root.CreateContainerAsync<StackContainerNode>("TransitionRollback", mount);
            ui.Register<AdapterFadeNode>("res://Scenes/Settings/Graphics.tscn");
            var retained = await stack.PushAsync<AdapterFadeNode>();
            var transitionFailure = new InvalidOperationException("Expected adapter transition rollback failure.");
            ui.Register(() => new UINode { Transition = new AdapterFailingTransition(transitionFailure) }, "res://Scenes/Settings/Graphics.tscn");
            try
            {
                await stack.PushAsync<UINode>();
                throw new Exception("Transition should have failed.");
            }
            catch (InvalidOperationException ex)
            {
                Check(ReferenceEquals(ex, transitionFailure), "Transition replaced the original exception.");
                Check(ReferenceEquals(stack.Current, retained) && retained.IsVisible && retained.GetControl().Modulate.A == 1,
                    "Rollback restored visibility but left the previous fade transparent.");
            }
            await stack.DisposeAsync();
            // Godot frees QueueFree-owned scenes at frame end before this borrowed mount is rebound.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

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

    private sealed class AdapterFadeNode : UINode
    {
        protected override Task OnInitializeAsync(CancellationToken token)
        { Transition = new GodotFadeTransition(0.02); return Task.CompletedTask; }
    }
    private sealed class AdapterFailingTransition(Exception original) : IUITransition
    {
        public Task PlayAsync(IUIElement? element, bool entering, CancellationToken token) => Task.FromException(original);
        public void Restore(IUIElement? element, bool visible) { }
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
