using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Godot;

public partial class TutorialHost : Control
{
    private readonly List<Task> _tasks = new();
    public UIContext UI { get; private set; } = null!;
    public StackContainerNode Pages { get; private set; } = null!;
    public ModalContainerNode Dialogs { get; private set; } = null!;
    public Task Initialization { get; private set; } = null!;
    private bool _stopping;

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = false;
        Initialization = InitializeAsync();
        Observe(Initialization);
    }
    private async Task InitializeAsync()
    {
        UI = GodotUI.Create(this);
        Pages = await UI.Root.CreateContainerAsync<StackContainerNode>("Pages", GetNode<Control>("Pages"));
        Dialogs = await UI.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", GetNode<Control>("Dialogs"));
        Dialogs.ConfigureBackdrop(GodotBackdrops.Dim(Observe));
        UI.Register(() => new MenuView(OpenSettingsAsync, AskQuitAsync, Observe), "res://Menu.tscn");
        UI.Register(() => new SettingsView(Dialogs, Observe), "res://Settings.tscn");
        UI.Register<ConfirmView>("res://Confirm.tscn");
        await Pages.PushAsync<MenuView>();
    }
    public void Observe(Task task) { _tasks.Add(task); }
    public override void _Process(double delta)
    {
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            var task = _tasks[i];
            if (!task.IsCompleted) continue;
            _tasks.RemoveAt(i);
            task.GetAwaiter().GetResult();
        }
    }
    public Task OpenSettingsAsync() => Dialogs.OpenAsync<SettingsView>();
    public async Task AskQuitAsync()
    {
        // false is an explicit answer; external closure cancels the waiting task.
        if (await Dialogs.ShowDialogAsync<ConfirmView, bool>()) await ShutdownAsync(true);
    }
    public async Task ShutdownAsync(bool quit)
    {
        _stopping = true;
        await UI.DisposeAsync();
        if (quit) GetTree().Quit();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (!input.IsActionPressed("ui_cancel") || input.IsEcho() || _stopping || !Initialization.IsCompletedSuccessfully) return;
        if (Dialogs.Current is ConfirmView confirm && confirm.IsInteractive) { confirm.Answer(false); GetViewport().SetInputAsHandled(); }
        else if (Dialogs.Current is SettingsView settings && settings.IsInteractive && !settings.Switching)
        { Observe(Dialogs.CloseAsync(settings)); GetViewport().SetInputAsHandled(); }
        else if (Dialogs.Current == null && Pages.IsInteractive)
        { Observe(AskQuitAsync()); GetViewport().SetInputAsHandled(); }
    }
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && !_stopping && Initialization.IsCompletedSuccessfully && Dialogs.Current == null && Pages.IsInteractive)
            Observe(AskQuitAsync());
    }
}

public sealed class MenuView : UINode
{
    private readonly Func<Task> _settings, _quit;
    private readonly Action<Task> _observe;
    private Button _settingsButton = null!, _quitButton = null!;
    public MenuView(Func<Task> settings, Func<Task> quit, Action<Task> observe)
    { _settings = settings; _quit = quit; _observe = observe; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _settingsButton = this.GetControl<Button>("Content/Settings");
        _quitButton = this.GetControl<Button>("Content/Quit");
        _settingsButton.Pressed += () => _observe(_settings());
        _quitButton.Pressed += () => _observe(_quit());
        return Task.CompletedTask;
    }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput() { _settingsButton.Disabled = !IsInteractive; _quitButton.Disabled = !IsInteractive; }
}

public sealed class SettingsView : UINode
{
    private readonly ModalContainerNode _dialogs;
    private readonly Action<Task> _observe;
    private TabContainerNode _tabs = null!;
    private Button _audio = null!, _graphics = null!, _close = null!;
    public bool Switching { get; private set; }
    public string? Selected => _tabs.CurrentName;
    public SettingsView(ModalContainerNode dialogs, Action<Task> observe) { _dialogs = dialogs; _observe = observe; }
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        _audio = this.GetControl<Button>("Content/AudioTab");
        _graphics = this.GetControl<Button>("Content/GraphicsTab");
        _close = this.GetControl<Button>("Content/Close");
        _tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", this.GetControl<Control>("Content/Body"), token);
        await _tabs.BindAsync("Audio", this.GetControl<Control>("Content/Body/Audio"), token);
        await _tabs.BindAsync("Graphics", this.GetControl<Control>("Content/Body/Graphics"), token);
        await _tabs.SwitchAsync("Audio", playAnimation: false, cancellationToken: token);
        _audio.Pressed += () => _observe(SwitchAsync("Audio"));
        _graphics.Pressed += () => _observe(SwitchAsync("Graphics"));
        _close.Pressed += () => _observe(_dialogs.CloseAsync(this));
    }
    public async Task SwitchAsync(string name)
    {
        Switching = true; RefreshInput();
        try { if (_tabs.CurrentName != name) await _tabs.SwitchAsync(name); }
        finally { Switching = false; if (State == NodeState.Ready) RefreshInput(); }
    }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput()
    { bool disabled = !IsInteractive || Switching; _audio.Disabled = disabled; _graphics.Disabled = disabled; _close.Disabled = disabled; }
}

public sealed class ConfirmView : DialogNode<bool>
{
    private Button _yes = null!, _no = null!;
    private bool _answered;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _yes = this.GetControl<Button>("Content/Yes"); _no = this.GetControl<Button>("Content/No");
        _yes.Pressed += () => Answer(true); _no.Pressed += () => Answer(false);
        return Task.CompletedTask;
    }
    public void Answer(bool value) { Complete(value); _answered = true; RefreshInput(); }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput() { _yes.Disabled = !IsInteractive || _answered; _no.Disabled = !IsInteractive || _answered; }
}
