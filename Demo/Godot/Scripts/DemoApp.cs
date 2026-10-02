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
        _ui.DefaultTransition = GodotTransitions.Fade(0.12);
        _ui.RegisterGeneratedUI();
        _menu = new DemoMenuNode();
        await _menu.AttachToAsync(_ui.Root, "DemoMenu", GetNode<Control>("Frame/Layout/Workspace/Sidebar"));
        _stage = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoStage", _content);
        _cards = await _ui.Root.CreateContainerAsync<PanelContainerNode>("DemoCards", GetNode<Control>("FloatingMount"));
        Modals = await _ui.Root.CreateContainerAsync<ModalContainerNode>("DemoDialogs", GetNode<Control>("ModalMount"));
        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared);
        Wire("Tabs", OpenPreferencesAsync);
        Wire("Inventory", OpenInventoryAsync);
        Wire("Settings", () => Modals.OpenSettingsWindowViewAsync());
        Wire("Instances", OpenCardsAsync);
        Wire("Confirm", ConfirmAsync);
        Wire("Cancel", OpenHomeAsync);
        if (OS.GetCmdlineUserArgs().Contains("--self-test")) await RunSelfTestAsync();
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
        else if (OS.GetCmdlineUserArgs().Contains("--failure-window-parameters"))
        {
            await OpenInventoryAsync();
            await _stage.OpenAsync<InventoryWindowView>(new object());
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-window-state"))
        {
            await OpenInventoryAsync();
            await _stage.ShowAsync(_scenario!);
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-stack-target"))
        {
            await OpenInventoryAsync();
            await _inventoryStack!.PopToAsync(_inventoryStack.Current!);
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-return-cleanup"))
        {
            await OpenInventoryAsync();
            var inventory = _inventoryStack!.Current!;
            _ui.Register(() => new FailureView("cleanup"), "res://Scenes/Settings/Graphics.tscn");
            await _inventoryStack.PushAsync<FailureView>();
            await _inventoryStack.PushProvenanceViewAsync();
            await _inventoryStack.PopToAsync(inventory);
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-transition-target"))
        {
            _ui.Register(() => new UINode { Transition = GodotTransitions.Fade(targetPath: "MissingAnimationTarget") }, "res://Scenes/Settings/Graphics.tscn");
            await _stage.OpenAsync<UINode>();
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-backdrop"))
        {
            Modals.ConfigureBackdrop(new FailingBackdropFactory(new IOException("Intentional original backdrop creation exception.")));
            await Modals.OpenSettingsWindowViewAsync();
        }
        else if (OS.GetCmdlineUserArgs().Contains("--failure-backdrop-dismiss"))
        {
            var window = await Modals.OpenSettingsWindowViewAsync();
            window.DismissRequested = _ => Task.FromException(new IOException("Intentional original backdrop dismiss exception."));
            await Modals.RequestDismissAsync();
        }
        else if (OS.GetCmdlineUserArgs().Any(a => new[] { "--failure-selector", "--failure-selector-null", "--failure-selector-cycle" }.Contains(a)))
        {
            var mode = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--failure-selector"));
            IUITransition? selected = null;
            selected = Transitions.Select(_ => mode switch
            {
                "--failure-selector" => throw new IOException("Intentional original selector exception."),
                "--failure-selector-null" => null!,
                "--failure-selector-cycle" => selected!,
                _ => throw new ArgumentException(mode)
            });
            _ui.Register(() => new UINode { Transition = selected }, "res://Scenes/Settings/Graphics.tscn");
            await _stage.OpenAsync<UINode>();
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

    public Task OpenInventoryAsync() => ChangeScenarioAsync(async () =>
    {
        var window = await _stage.OpenInventoryWindowViewAsync();
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

        var inventoryWindow = (InventoryWindowView)_scenario!;
        var originalDisplay = inventoryWindow.GetControl();
        var windowEnters = inventoryWindow.EnterCount;
        await OpenInventoryAsync();
        Check(ReferenceEquals(inventoryWindow, _scenario) && inventoryWindow.EnterCount == windowEnters,
            "Repeated backpack opening replayed entry or created another window.");
        await OpenHomeAsync(); inspection = inventory.PreviewSeconds; inventory.Tick(1);
        Check(!inventoryWindow.IsVisible && inventory.PreviewSeconds == inspection, "Stowed backpack kept updating.");
        Check(_stage.TryGet<InventoryWindowView>(out var hidden) && ReferenceEquals(hidden, inventoryWindow), "Hidden single window was lost.");
        await OpenInventoryAsync();
        Check(ReferenceEquals(originalDisplay, inventoryWindow.GetControl()) && inventoryWindow.InitializeCount == 1,
            "Restoring backpack loaded another scene or initialized again.");
        Check(ReferenceEquals(inventory, _inventoryStack.Current) && inventory.Tabs.CurrentName == "Materials",
            "Window restoration lost inner stack or tab.");
        await _stage.HideAsync(inventoryWindow); await _stage.ShowAsync(inventoryWindow);
        Check(inventory.IsActive, "Explicit Show did not reactivate backpack subtree.");
        GD.Print("PASS V3 single window: repeated open, retained hide/show, subtree pause/resume");

        await inventory.InspectAsync("Moonstone");
        var skippedDetails = (DetailsView)_inventoryStack.Current!;
        var notes = await _inventoryStack.PushProvenanceViewAsync();
        var detailsEnters = skippedDetails.EnterCount;
        await OpenHomeAsync(); await OpenInventoryAsync();
        Check(ReferenceEquals(notes, _inventoryStack.Current), "Stowing a nested page lost its history.");
        await _inventoryStack.PopToAsync(inventory);
        Check(ReferenceEquals(inventory, _inventoryStack.Current) && _inventoryStack.History.Count == 1,
            "Target return did not commit the original backpack.");
        Check(skippedDetails.EnterCount == detailsEnters && skippedDetails.State == NodeState.Disposed && notes.State == NodeState.Disposed,
            "Target return resumed an intermediate page or left it owned.");
        Check(inventory.SelectedItem == "Moonstone" && inventory.Tabs.CurrentName == "Materials", "Target return lost game UI state.");
        GD.Print("PASS V3 target return: field notes -> original backpack, intermediate pages released");

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
        Check(Modals.CurrentBackdrop != null, "Managed backdrop is missing.");
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
        var sharedBackdrop = Modals.CurrentBackdrop!;
        var outerFocus = GetViewport().GuiGetFocusOwner();
        var innerResult = Modals.ShowDialogAsync<ConfirmView, bool>();
        await WaitUntilAsync(() => Modals.Current is ConfirmView view && view != outer && view.IsInteractive);
        var inner = (ConfirmView)Modals.Current!;
        Check(ReferenceEquals(sharedBackdrop, Modals.CurrentBackdrop), "Nested modal created a second shared backdrop.");
        var dim = GetNode<Control>("ModalMount/ModalBackdrop");
        Check(dim.GetIndex() < inner.GetControl().GetIndex() && dim.GetIndex() > outer.GetControl().GetIndex(),
            "Shared backdrop must sit between the outer and inner dialogs.");
        Check(!outer.IsInteractive, "Nested modal did not block its parent.");
        dim.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
        Check(!await innerResult, "Backdrop click must yield the normal false result.");
        Check(outer.IsInteractive, "Outer modal did not regain input.");
        Check(GetViewport().GuiGetFocusOwner() == outerFocus, "Nested modal focus was not restored.");
        outer.Complete(true); Check(await outerResult, "Outer result.");
        GD.Print("PASS nested modal results");
        Check(Modals.CurrentBackdrop == null, "Last dialog left a backdrop behind.");
        GD.Print("PASS V4 shared backdrop: one display, ordering, native click cancellation, final release");

        var clickSettings = await Modals.OpenSettingsWindowViewAsync();
        Check(await Modals.RequestDismissAsync(), "Settings did not handle its external click request.");
        Check(clickSettings.State == NodeState.Disposed && Modals.Current == null, "External click did not close settings.");
        GD.Print("PASS V4 settings external dismissal");

        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track, duration: 0.02), ModalBackdropMode.PerModal);
        var layeredOuter = await Modals.OpenAsync<ConfirmView>(); var outerBackdrop = Modals.CurrentBackdrop;
        var layeredInner = await Modals.OpenAsync<ConfirmView>(); var innerBackdrop = Modals.CurrentBackdrop;
        Check(!ReferenceEquals(outerBackdrop, innerBackdrop), "PerModal reused one backdrop.");
        var liveDims = GetNode<Control>("ModalMount").GetChildren().OfType<ColorRect>().Where(c => !c.IsQueuedForDeletion()).ToArray();
        Check(liveDims.Length == 2 && liveDims.All(c => c.Visible), "PerModal must own two visible dim layers.");
        Check((int)outerBackdrop!.CaptureOrder() < layeredOuter.GetControl().GetIndex()
            && layeredOuter.GetControl().GetIndex() < (int)innerBackdrop!.CaptureOrder()
            && (int)innerBackdrop.CaptureOrder() < layeredInner.GetControl().GetIndex(), "PerModal layer order is incorrect.");
        await Modals.CloseAsync(layeredInner); Check(ReferenceEquals(Modals.CurrentBackdrop, outerBackdrop) && layeredOuter.IsInteractive, "PerModal did not restore outer input/backdrop.");
        await layeredOuter.DisposeAsync(); Check(Modals.CurrentBackdrop == null && _menu.IsInteractive, "Forced modal disposal left its backdrop/input owner.");
        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared);
        GD.Print("PASS V4 layered backdrops: distinct displays, ordering, nested close and forced release");

        await OpenHomeAsync(); await _stage.CloseAsync(inventoryWindow);
        Check(inventoryWindow.State == NodeState.Disposed && inventory.State == NodeState.Disposed,
            "Closing hidden backpack did not release its subtree.");
        Check(!_stage.TryGet<InventoryWindowView>(out _), "Closed backpack remained in the instance index.");
        await OpenInventoryAsync();
        Check(!ReferenceEquals(inventoryWindow, _scenario) && ((InventoryWindowView)_scenario!).InitializeCount == 1,
            "Reopening a closed backpack reused a disposed instance.");
        GD.Print("PASS V3 hidden close: subtree released, next open creates fresh window");

        await RunTransitionSelectionSelfTestAsync();
        await CancelLoadingAsync();
        await _ui.DisposeAsync();
        Check(_ui.Root.Children.Count == 0, "Root cleanup left children.");
        GD.Print("GODOT DEMO SELF-TEST PASSED (15 scenarios)");
        _exiting = true;
        GetTree().Quit(0);
    }

    private async Task RunTransitionSelectionSelfTestAsync()
    {
        await OpenPreferencesAsync(); var preferences = (PreferencesView)_scenario!;
        var contexts = new List<TransitionContext>(); var tabRecipe = preferences.Tabs.ChildTransition!;
        preferences.Tabs.ChildTransition = Transitions.Select(c => { contexts.Add(c); return tabRecipe; });
        await preferences.SelectAsync("Audio", playAnimation: false);
        Check(contexts.Count == 0 && preferences.Tabs.CurrentName == "Audio", "Instant Tab switch invoked selection or failed to commit.");
        await preferences.SelectAsync("Graphics");
        Check(contexts.Select(c => c.Phase).SequenceEqual(new[] { TransitionPhase.Exit, TransitionPhase.Enter })
            && contexts.All(c => c.Source?.Name == "Audio" && c.Target?.Name == "Graphics"), "Tab selection context lost phases or names.");
        GD.Print("PASS V5 preferences: initial/instant pages, name-based selection, normal transitions");

        await OpenInventoryAsync(); var stack = ((InventoryWindowView)_scenario!).Body; var inventory = (InventoryView)stack.Current!;
        contexts.Clear(); var stackRecipe = stack.ChildTransition!;
        stack.ChildTransition = Transitions.Select(c => { contexts.Add(c); return stackRecipe; });
        await inventory.InspectAsync("Moonstone"); var details = stack.Current!;
        Check(contexts.Select(c => c.Phase).SequenceEqual(new[] { TransitionPhase.PushExit, TransitionPhase.PushEnter })
            && contexts.All(c => ReferenceEquals(c.Source, inventory) && ReferenceEquals(c.Target, details)), "Stack selection did not receive source/target phases.");
        contexts.Clear(); await stack.PushProvenanceViewAsync(); var notes = stack.Current!;
        Check(contexts.All(c => c.Source is DetailsView && c.Target is ProvenanceView), "Field note transition lost its business types.");
        contexts.Clear(); await stack.PopAsync();
        Check(contexts.Select(c => c.Phase).SequenceEqual(new[] { TransitionPhase.PopExit, TransitionPhase.PopEnter }), "Return selection phases.");
        contexts.Clear(); await stack.PopToAsync(inventory, playAnimation: false);
        Check(contexts.Count == 0 && ReferenceEquals(stack.Current, inventory) && details.State == NodeState.Disposed && notes.State == NodeState.Disposed,
            "Instant return selected animation or failed to release intermediate pages.");
        GD.Print("PASS V5 backpack: phase/type selection, reverse return, instant return cleanup");

        foreach (var mode in new[] { ModalBackdropMode.Shared, ModalBackdropMode.PerModal })
        {
            Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track, duration: 2), mode);
            var opening = Modals.OpenSettingsWindowViewAsync(playAnimation: false);
            Check(opening.IsCompletedSuccessfully, "Instant modal waited for its node or dim animation."); var window = await opening;
            var dim = window.GetControl().GetParent().GetChildren().OfType<ColorRect>().Single(c => !c.IsQueuedForDeletion());
            Check(dim.Visible && dim.Modulate.A == 1 && Modals.Current == window && window.IsInteractive, "Instant backdrop/input final state.");
            var closing = Modals.CloseAsync(window, playAnimation: false); Check(closing.IsCompletedSuccessfully, "Instant close waited for animation.");
            await closing; Check(Modals.CurrentBackdrop == null && window.State == NodeState.Disposed && _menu.IsInteractive, "Instant modal cleanup or input restoration.");
        }
        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track));
        var dialog = Modals.ShowConfirmViewAsync(playAnimation: false); ((ConfirmView)Modals.Current!).Complete(false);
        Check(!await dialog && Modals.Current == null, "Generated instant dialog failed to return the explicit cancel result.");
        GD.Print("PASS V5 modal: instant shared/per-modal dim, lifecycle/input/cleanup, generated dialog result");
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
            await VerifyNativeTransitionsAsync(tabs.Current!, page);
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

    private async Task VerifyNativeTransitionsAsync(UINode node, Control page)
    {
        page.Size = new Vector2(100, 70); page.Position = new Vector2(5, 7);
        var tint = new Color(0.7f, 0.8f, 0.9f, 0.6f); page.Modulate = tint;
        var position = page.Position; var scale = new Vector2(1.1f, 0.9f); page.Scale = scale;
        var pivot = new Vector2(3, 4); page.PivotOffset = pivot;
        var lifecycle = new LifecycleContext(node, node.Parent as ContainerNode, LifecycleReason.Push, null, node);
        foreach (var direction in new[] { TransitionDirection.Forward, TransitionDirection.Backward })
        {
            var run = GodotTransitions.SlideHorizontal(0.02).Create(new(lifecycle, true, direction));
            var playing = run.PlayAsync(CancellationToken.None);
            var sign = direction == TransitionDirection.Forward ? 1 : -1;
            Check(page.Position.IsEqualApprox(position + new Vector2(sign * page.Size.X, 0)), "Slide did not honor the operation direction.");
            await playing; run.Restore(); Check(page.Position.IsEqualApprox(position), "Slide drifted after restoration.");
        }
        var fade = GodotTransitions.Fade(0.02).Create(new(lifecycle, true, TransitionDirection.None));
        await fade.PlayAsync(CancellationToken.None); fade.Restore(); Check(page.Modulate == tint, "Fade overwrote authored tint/alpha.");
        var zoom = GodotTransitions.ScaleFade(0.02).Create(new(lifecycle, true, TransitionDirection.None));
        await zoom.PlayAsync(CancellationToken.None); zoom.Restore();
        Check(page.Scale == scale && page.PivotOffset == pivot && page.Modulate == tint, "ScaleFade did not restore authored presentation.");
        using var canceled = new CancellationTokenSource();
        var rollback = GodotTransitions.ScaleFade(0.2).Create(new(lifecycle, false, TransitionDirection.None));
        var pending = rollback.PlayAsync(canceled.Token); canceled.Cancel();
        await ExpectCancellationAsync(pending); rollback.Restore(); Check(page.Scale == scale && page.Modulate == tint, "Canceled scale animation leaked state.");

        var player = new AnimationPlayer { Name = "Animator" }; page.AddChild(player);
        var library = new AnimationLibrary();
        library.AddAnimation("enter", new Animation { Length = 0.02 }); library.AddAnimation("exit", new Animation { Length = 0.02 });
        player.AddAnimationLibrary("", library);
        var restored = false;
        var authored = GodotTransitions.Animation("Animator", "enter", "exit", c => { c.Modulate = tint; restored = true; });
        var animation = authored.Create(new(lifecycle, true, TransitionDirection.None));
        await animation.PlayAsync(CancellationToken.None); animation.Restore(); Check(restored && !player.IsPlaying(), "AnimationPlayer restoration was not invoked.");
        GD.Print("PASS V4 native animations: slide directions, tint/position/scale restoration, cancellation, AnimationPlayer");
    }

    private sealed class AdapterFadeNode : UINode
    {
        protected override Task OnInitializeAsync(CancellationToken token)
        { Transition = GodotTransitions.Fade(0.02); return Task.CompletedTask; }
    }
    private sealed class FailingBackdropFactory(Exception failure) : IUIBackdropFactory
    {
        public Task<IUIBackdrop> CreateAsync(IUIElement? mount, Func<Task> dismiss, CancellationToken token) => Task.FromException<IUIBackdrop>(failure);
    }
    private sealed class AdapterFailingTransition(Exception original) : IUITransition, IUITransitionRun
    {
        public Task PlayAsync(CancellationToken token) => Task.FromException(original);
        public IUITransitionRun Create(TransitionContext context) => this; public void Restore() { }
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
