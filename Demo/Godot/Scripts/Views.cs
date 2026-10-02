using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Generated;

namespace HierarchyDemo;

public abstract class DemoView : UINode
{
    private readonly List<Action> _disconnect = new();
    public int EnterCount { get; private set; }
    public int ActivationCount { get; private set; }
    public int DeactivationCount { get; private set; }
    protected override void OnEntered(LifecycleContext context)
    {
        EnterCount++;
        GD.Print($"UI {GetType().Name}/{Id} entered {context.Reason}, first={context.IsFirstEnter}");
    }
    protected override void OnActivated(StateChangeContext context) { ActivationCount++; }
    protected override void OnDeactivated(StateChangeContext context) { DeactivationCount++; }
    public GodotUIElement Display => (GodotUIElement)Element!;
    protected void Wire(string path, Func<Task> action)
    {
        var button = this.GetControl<Button>(path);
        void Handler() => DemoApp.Instance.Track(action());
        button.Pressed += Handler;
        _disconnect.Add(() => button.Pressed -= Handler);
    }
    protected void WireSlider(string path, Action<double> action)
    {
        var slider = this.GetControl<HSlider>(path);
        void Handler(double value) => DemoApp.Instance.TrackAction(() => action(value));
        slider.ValueChanged += Handler;
        _disconnect.Add(() => slider.ValueChanged -= Handler);
    }
    protected void WireToggle(string path, Action<bool> action)
    {
        var button = this.GetControl<CheckButton>(path);
        void Handler(bool value) => DemoApp.Instance.TrackAction(() => action(value));
        button.Toggled += Handler;
        _disconnect.Add(() => button.Toggled -= Handler);
    }
    protected void ObserveTransition(Action<TransitionState> observer)
    {
        TransitionChanged += observer;
        _disconnect.Add(() => TransitionChanged -= observer);
    }
    protected override Task OnDisposeAsync()
    {
        foreach (var disconnect in _disconnect) disconnect();
        _disconnect.Clear(); return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Preferences/Preferences.tscn")]
public class PreferencesView : DemoView
{
    public TabContainerNode Tabs { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Tabs", this.GetControl<Control>("TabContent"), token);
        var fade = GodotTransitions.Fade(0.10);
        var scale = GodotTransitions.ScaleFade(0.16, fromScale: 0.98f, delay: 0.02,
            transition: Tween.TransitionType.Back, ease: Tween.EaseType.Out);
        Tabs.ChildTransition = Transitions.Select(c => c.Phase == TransitionPhase.Enter &&
            c.Source?.Name == "Audio" && c.Target?.Name == "Graphics" ? scale : fade);
        foreach (var name in new[] { "Graphics", "Audio", "Controls" })
        {
            var page = new UINode();
            page.AddLifecycle(new PageActivity());
            await Tabs.BindAsync(name, page, this.GetControl<Control>($"TabContent/{name}"), token);
            Wire($"TabButtons/{name}", () => SelectAsync(name));
        }
        var model = DemoApp.Instance.GameState;
        this.GetControl<HSlider>("TabContent/Graphics/Value").SetValueNoSignal(model.RenderScale);
        this.GetControl<CheckButton>("TabContent/Graphics/Enabled").SetPressedNoSignal(model.Shadows);
        this.GetControl<HSlider>("TabContent/Audio/Value").SetValueNoSignal(model.MasterVolume);
        this.GetControl<CheckButton>("TabContent/Audio/Enabled").SetPressedNoSignal(model.Music);
        this.GetControl<HSlider>("TabContent/Controls/Value").SetValueNoSignal(model.CameraSensitivity);
        this.GetControl<CheckButton>("TabContent/Controls/Enabled").SetPressedNoSignal(model.InvertCamera);
        WireSlider("TabContent/Graphics/Value", value => model.RenderScale = value);
        WireToggle("TabContent/Graphics/Enabled", value => model.Shadows = value);
        WireSlider("TabContent/Audio/Value", value => model.MasterVolume = value);
        WireToggle("TabContent/Audio/Enabled", value => model.Music = value);
        WireSlider("TabContent/Controls/Value", value => model.CameraSensitivity = value);
        WireToggle("TabContent/Controls/Enabled", value => model.InvertCamera = value);
        await SelectAsync("Graphics", playAnimation: false, token: token);
    }
    public async Task SelectAsync(string name, bool playAnimation = true, CancellationToken token = default)
    {
        await Tabs.SwitchAsync(name, playAnimation, cancellationToken: token);
        foreach (var key in Tabs.Items.Keys)
            this.GetControl<Button>($"TabButtons/{key}").Disabled = key == name;
        DemoApp.Instance.Status($"Preferences / {name}");
    }
}

[UIResource("res://Scenes/Settings/SettingsWindow.tscn")]
public class SettingsWindowView : DemoView
{
    public StackContainerNode Body { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        var neutral = new GodotTransitionFrame();
        var enter = GodotTransitions.Simple(neutral with { Alpha = 0, Scale = Vector2.One * .96f, Offset = new Vector2(0, 12) },
            neutral, duration: .18, delay: .02, targetPath: "Center", transition: Tween.TransitionType.Quart, ease: Tween.EaseType.Out);
        var exit = GodotTransitions.Simple(neutral, neutral with { Alpha = 0, Scale = Vector2.One * .98f },
            duration: .12, targetPath: "Center");
        Transition = Transitions.Select(c => c.Entering ? enter : exit);
        DismissRequested = ct => DemoApp.Instance.Modals.CloseAsync(this, cancellationToken: ct);
        Body = await this.CreateContainerAsync<StackContainerNode>("Body", this.GetControl<Control>("Center/Window/Margin/BodyMount"), token);
        Body.ChildTransition = GodotTransitions.Fade();
        await Body.PushSettingsViewAsync(playAnimation: false, cancellationToken: token);
    }
}

[UIResource("res://Scenes/Settings/Settings.tscn")]
public class SettingsView : DemoView
{
    protected override void OnEntered(LifecycleContext context)
    {
        base.OnEntered(context);
        this.GetControl<Label>("Description").Text = context.IsFirstEnter
            ? "Tune your adventure. Changes stay with this window."
            : "Welcome back. Your graphics preferences have been kept.";
    }
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
        var model = DemoApp.Instance.GameState;
        this.GetControl<HSlider>("Value").SetValueNoSignal(model.RenderScale);
        this.GetControl<CheckButton>("Enabled").SetPressedNoSignal(model.Shadows);
        WireSlider("Value", value => model.RenderScale = value);
        WireToggle("Enabled", value => model.Shadows = value);
        Wire("Back", () => ((StackContainerNode)Parent!).PopAsync());
        return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Inventory/InventoryWindow.tscn")]
public class InventoryWindowView : DemoView
{
    public int InitializeCount { get; private set; }
    public StackContainerNode Body { get; private set; } = null!;
    public InventoryView Inventory { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        InitializeCount++;
        Transition = GodotTransitions.Fade();
        Body = await this.CreateContainerAsync<StackContainerNode>("Body", this.GetControl<Control>("BodyMount"), token);
        var slide = GodotTransitions.SlideHorizontal();
        var fade = GodotTransitions.Fade(0.12);
        var listExit = GodotTransitions.Fade(.14);
        var listEnter = GodotTransitions.Fade(.24, delay: .24);
        var partner = new ItemPartnerTransition();
        var neutral = new GodotTransitionFrame();
        var notesEnter = GodotTransitions.Simple(neutral with { Offset = new Vector2(0, 12), Alpha = 0 }, neutral,
            duration: .18, delay: .03, transition: Tween.TransitionType.Quart, ease: Tween.EaseType.Out);
        var notesExit = GodotTransitions.Simple(neutral, neutral with { Offset = new Vector2(0, -12), Alpha = 0 }, duration: .12);
        Body.ChildTransition = Transitions.Select(c =>
            (c.Source is InventoryView && c.Target is DetailsView || c.Source is DetailsView && c.Target is InventoryView)
                ? c.Node is DetailsView ? partner : c.Entering ? listEnter : listExit
            : c.Node is ProvenanceView ? c.Entering ? notesEnter : notesExit
            : c.Source is ProvenanceView || c.Target is ProvenanceView ? fade : c.Phase switch
            {
                TransitionPhase.PushEnter or TransitionPhase.PopExit => slide,
                _ => fade
            });
        Inventory = await Body.PushInventoryViewAsync(playAnimation: false, cancellationToken: token);
        Wire("Actions/Stow", () => DemoApp.Instance.OpenHomeAsync());
    }
}

[UIResource("res://Scenes/Inventory/Inventory.tscn")]
public class InventoryView : DemoView
{
    public HashSet<string> Favorites => DemoApp.Instance.GameState.Favorites;
    public string SelectedItem { get; private set; } = "Oakheart Bow";
    public TextureRect SelectedIcon { get; private set; } = null!;
    public double PreviewSeconds { get; private set; }
    public void Tick(double delta)
    {
        if (Tabs.Current?.IsActive != true) return;
        PreviewSeconds += delta;
        this.GetControl<Label>("Description").Text = $"LYRA / Ranger     |     1,240 gold     |     Inspection time {PreviewSeconds:0.0}s";
    }
    public TabContainerNode Tabs { get; private set; } = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Tabs", this.GetControl<Control>("TabContent"), token);
        foreach (var name in new[] { "Equipment", "Materials" })
        {
            var page = new UINode();
            page.AddLifecycle(new PageActivity());
            await Tabs.BindAsync(name, page, this.GetControl<Control>($"TabContent/{name}"), token);
            Wire($"TabButtons/{name}", () => SelectAsync(name));
            WireToggle($"TabContent/{name}/Enabled", _ => RefreshFavorites());
            Wire($"TabContent/{name}/Details", () => InspectFromAsync(name == "Equipment" ? "Oakheart Bow" : "Moonstone",
                this.GetControl<TextureRect>($"TabContent/{name}/Details/Icon")));
            for (var i = 0; i < 6; i++)
            {
                var button = this.GetControl<Button>($"TabContent/{name}/Items/Item{i}");
                Wire($"TabContent/{name}/Items/Item{i}", () => InspectAsync(button.Text.Split(" x")[0]));
            }
        }
        await SelectAsync("Equipment", playAnimation: false, token: token);
        SelectedIcon = this.GetControl<TextureRect>("TabContent/Equipment/Items/Item0/Icon");
    }
    protected override void OnEntered(LifecycleContext context)
    {
        base.OnEntered(context);
        RefreshFavorites();
    }
    private void RefreshFavorites()
    {
        foreach (var name in Tabs.Items.Keys)
        {
            var only = this.GetControl<CheckButton>($"TabContent/{name}/Enabled").ButtonPressed;
            for (var i = 0; i < 6; i++)
            {
                var button = this.GetControl<Button>($"TabContent/{name}/Items/Item{i}");
                button.Visible = !only || Favorites.Contains(button.Text.Split(" x")[0]);
            }
        }
    }
    public Task InspectAsync(string item, bool playAnimation = true, CancellationToken token = default)
    {
        var buttons = this.GetControl<Control>($"TabContent/{Tabs.CurrentName}/Items").GetChildren().OfType<Button>();
        var button = buttons.Single(b => b.Text.Split(" x")[0] == item);
        if (!button.Visible) throw new InvalidOperationException("Cannot inspect a hidden item; use the visible inspection action.");
        return InspectFromAsync(item, button.GetNode<TextureRect>("Icon"), playAnimation, token);
    }
    private Task InspectFromAsync(string item, TextureRect icon, bool playAnimation = true, CancellationToken token = default)
    {
        SelectedItem = item;
        SelectedIcon = icon;
        return ((StackContainerNode)Parent!).PushDetailsViewAsync(playAnimation: playAnimation, cancellationToken: token);
    }
    public async Task SelectAsync(string name, bool playAnimation = true, CancellationToken token = default)
    {
        await Tabs.SwitchAsync(name, playAnimation, cancellationToken: token);
        foreach (var key in Tabs.Items.Keys) this.GetControl<Button>($"TabButtons/{key}").Disabled = key == name;
        DemoApp.Instance.Status($"Backpack / {name} / Select an item to inspect.");
    }
}

[UIResource("res://Scenes/Inventory/Details.tscn")]
public class DetailsView : DemoView
{
    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var inventory = (InventoryView)((StackContainerNode)Parent!).History[0];
        var equipment = inventory.Tabs.CurrentName == "Equipment";
        this.GetControl<CheckButton>("Enabled").SetPressedNoSignal(inventory.Favorites.Contains(inventory.SelectedItem));
        this.GetControl<Label>("Title").Text = inventory.SelectedItem.ToUpperInvariant();
        this.GetControl<Label>("Artwork/Description").Text = equipment
            ? $"RARE / Equipment\n{inventory.SelectedItem} / Ranger collection\n\nCarved from the ancient forest.\nBound to Lyra; a keepsake from her first expedition."
            : $"UNCOMMON / Crafting material\n{inventory.SelectedItem} / Upgrade supplies\n\nA pale crystal found in the northern ruins.\nReserved for the next equipment upgrade.";
        this.GetControl<TextureRect>("Artwork/Icon").Texture = inventory.SelectedIcon.Texture;
        return Task.CompletedTask;
    }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        // Opening field notes reveals them beneath the departing detail page.
        TransitionOrder = 10;
        var hint = Parent!.Parent!.GetControl<Label>("Actions/Hint");
        var tint = hint.Modulate;
        ObserveTransition(state => hint.Modulate = state.Stage == TransitionStage.Playing
            ? new Color(tint.R, tint.G, tint.B, tint.A * (1 - .35f * state.Progress)) : tint);
        Wire("Notes", () => ((StackContainerNode)Parent!).PushProvenanceViewAsync());
        Wire("Back", () => ((StackContainerNode)Parent!).PopAsync());
        WireToggle("Enabled", favorite =>
        {
            var inventory = (InventoryView)((StackContainerNode)Parent!).History[0];
            if (favorite) inventory.Favorites.Add(inventory.SelectedItem);
            else inventory.Favorites.Remove(inventory.SelectedItem);
        });
        return Task.CompletedTask;
    }
}

[UIResource("res://Scenes/Inventory/Provenance.tscn")]
public class ProvenanceView : DemoView
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        var stack = (StackContainerNode)Parent!;
        var inventory = (InventoryView)stack.History[0];
        this.GetControl<Label>("Description").Text = $"{inventory.SelectedItem.ToUpperInvariant()}\n\n" +
            "Recovered along the northern trail.\nLyra recorded this find before returning to camp.\n\n" +
            "Keep this entry with your collection; your selection and favorites remain in the backpack.";
        Wire("Back", () => stack.PopAsync());
        Wire("Backpack", () => stack.PopToAsync(inventory));
        return Task.CompletedTask;
    }
}

public sealed record CardArgs(string Title, Vector2 Position);

[UIResource("res://Scenes/Inventory/Card.tscn", WindowMode = WindowInstanceMode.Multiple)]
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
        Transition = GodotTransitions.ScaleFade(targetPath: "Center");
        DismissRequested = ct => { ct.ThrowIfCancellationRequested(); Complete(false); return Task.CompletedTask; };
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

// Reusable lifecycle behavior on basic embedded Nodes, without empty View subclasses.
public sealed class PageActivity : NodeLifecycle
{
    public int Activations { get; private set; }
    public bool Running { get; private set; }
    public override void Activated(StateChangeContext context) { Running = true; Activations++; GD.Print($"Page {context.Node.Name} resume ({context.Operation.Reason})"); }
    public override void Deactivated(StateChangeContext context) { Running = false; GD.Print($"Page {context.Node.Name} pause ({context.Operation.Reason})"); }
    public override Task DisposeAsync(UINode node) { Running = false; return Task.CompletedTask; }
}
