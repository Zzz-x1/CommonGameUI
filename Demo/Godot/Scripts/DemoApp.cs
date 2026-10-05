using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Generated;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo;
public partial class DemoApp : Control
{
    public static DemoApp Instance { get; private set; } = null!;

    private readonly List<Task> _tasks = new();
    private readonly MvvmRuntime _mvvm = new();
    public ClientPlayerVm PlayerVm { get; private set; } = null!;
    private void RefreshPlayerVm() => PlayerVm.Refresh(Store.State);
    private UIContext _ui = null!;
    private PanelContainerNode _shells = null!;
    private LoginView? _login;
    public ClientStore Store { get; private set; } = null!;
    public ShellView Shell { get; private set; } = null!;
    public ModalContainerNode Modals { get; private set; } = null!;
    public PanelContainerNode Floating { get; private set; } = null!;
    public bool Busy { get; private set; }
    public Action? SkipCinematic { get; private set; }

    internal string Confirmation = "";
    private bool _exiting;
    public override void _Ready()
    {
        Instance = this;
        GetTree().AutoAcceptQuit = false;
        var args = OS.GetCmdlineUserArgs();
        var lifetimeTest = args.Contains("--lifetime-tests");
        var selfTest = args.Contains("--self-test") || lifetimeTest;
        var verifySave = args.Contains("--verify-client-save");
        var capture = args.Contains("--capture-client");
        var path = ProjectSettings.GlobalizePath(lifetimeTest ? "user://client-lifetime-test/save.json" : selfTest || verifySave ? "user://client-test/save.json" : capture ? "user://client-capture/save.json" : "user://client-save.json");
        if ((selfTest || capture) && File.Exists(path))
            File.Delete(path);
        Store = new ClientStore(path);
        ApplySettings();
        GetNode<Button>("CinematicGate/Skip").Pressed += () => SkipCinematic!();
        Track(StartAsync());
    }

    private async Task StartAsync()
    {
        _ui = GodotUI.Create(this);
        _ui.UseGodotMvvm(_mvvm);
        PlayerVm = _mvvm.GetSharedViewModel(GD.Load<ClientPlayerVm>("res://Assets/Mvvm/Player.tres"));
        RefreshPlayerVm();
        Store.Changed += RefreshPlayerVm;
        await new MvvmLifetime(this).AttachAsync(_ui.Root, "MvvmLifetime");
        _ui.BindLifetime(this);
        _ui.RegisterGeneratedUI();
        _ui.Register(() => new ConfirmView(Confirmation), "res://Scenes/Client/Modal.tscn");
        _ui.DefaultTransition = GodotTransitions.Fade(.18);
        _shells = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Client", GetNode<Control>("ShellMount"));
        Floating = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Comparison", GetNode<Control>("FloatingMount"));
        Modals = await _ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", GetNode<Control>("ModalMount"));
        Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared);
        _login = await _shells.OpenAsync<LoginView>();
        await RunDevelopmentAsync();
    }

    public async Task EnterAsync()
    {
        await RunAsync(_login!, async () =>
        {
            _login!.Text("Account", "正在准备星庭资源…");
            using var lobby = await _ui.PreloadAsync<LobbyView>();
            using var shell = await _ui.PreloadAsync<ShellView>();
            Store.Count("login");
            await _shells.HideAsync(_login);
            Shell = await _shells.OpenAsync<ShellView>();
            await Shell.EnterLobbyAsync();
        });
    }

    public Task OpenAsync(string destination) => RunAsync(async () =>
    {
        await CloseComparisonsAsync();
        if (destination == "Home")
        {
            if (Shell.Pages.Current != Shell.Home)
                await Shell.Pages.PopToAsync(Shell.Home);
            return;
        }

        // A repeated current destination is a normal navigation selection.
        if (Shell.Pages.Current is ClientPage page && page.Module == destination)
            return;
        switch (destination)
        {
            case "Characters":
                await Shell.Pages.PushAsync<CharactersView>();
                break;
            case "Inventory":
                await Shell.Pages.PushAsync<InventoryView>();
                break;
            case "Team":
                await Shell.Pages.PushAsync<TeamView>();
                break;
            case "Stages":
                await Shell.Pages.PushAsync<StagesView>();
                break;
            case "Shop":
                await Shell.Pages.PushAsync<ShopView>();
                break;
            case "Tasks":
                await Shell.Pages.PushAsync<TasksView>();
                break;
            case "Achievements":
                await Shell.Pages.PushAsync<AchievementsView>();
                break;
            case "Mail":
                await Shell.Pages.PushAsync<MailView>();
                break;
            case "Friends":
                await Shell.Pages.PushAsync<FriendsView>();
                break;
            case "Events":
                await Shell.Pages.PushAsync<EventsView>();
                break;
            default:
                throw new ArgumentException(destination);
        }
    });
    public async Task RunAsync(Func<Task> action)
    {
        if (Busy)
            throw new InvalidOperationException("客户端操作尚未完成。");
        Busy = true;
        if (Shell != null)
            Shell.SetNavigation(false);
        try
        {
            await action();
        }
        finally
        {
            Busy = false;
            if (Shell != null && Shell.State == NodeState.Ready)
                Shell.SetNavigation(true);
        }
    }

    public Task RunAsync(UINode owner, Func<Task> action) => RunAsync(async () =>
    {
        var display = (GodotUIElement)owner.Element!;
        var focus = owner.Context.Host.CaptureFocus();
        display.SetInteractive(false);
        try
        {
            await action();
        }
        finally
        {
            // A destroyed page no longer owns native controls to restore.
            if (owner.State == NodeState.Ready)
            {
                display.SetInteractive(owner.IsInteractive);
                owner.Context.Host.RestoreFocus(focus);
            }
        }
    });

    public async Task BackAsync()
    {
        if (Modals.Current != null)
        {
            await Modals.RequestDismissAsync();
            return;
        }

        if (Floating.Children.Count > 0)
        {
            await CloseComparisonsAsync();
            return;
        }

        if (Shell != null && Shell.Pages.Current != Shell.Home)
        {
            await RunAsync(() => Shell.Pages.PopAsync());
            return;
        }

        if (Shell != null && await AskAsync("返回标题？\n当前档案已保存。"))
        {
            await RunAsync(async () =>
            {
                await _shells.CloseAsync(Shell);
                Shell = null!;
                await _shells.ShowAsync(_login!);
            });
        }
    }

    public async Task<bool> AskAsync(string message)
    {
        Confirmation = message;
        return await Modals.ShowDialogAsync<ConfirmView, bool>();
    }

    public async Task ShutdownAsync()
    {
        if (_exiting)
            throw new InvalidOperationException("退出已在进行中。");
        _exiting = true;
        await _ui.DisposeAsync();
        Store.Changed -= RefreshPlayerVm;
        await _mvvm.DisposeAsync();
        GetTree().Quit();
    }

    private sealed class MvvmLifetime(DemoApp app) : UINode
    { protected override Task OnDisposeAsync() { app.Store.Changed -= app.RefreshPlayerVm; return app._mvvm.DisposeAsync().AsTask(); } }

    private bool CanHandleGlobalInput
    {
        get
        {
            if (_exiting || Busy || SkipCinematic != null || Modals is not { IsInteractive: true })
                return false;
            if (Modals.Current != null)
                return Modals.Current.IsInteractive;
            if (Floating is not { IsInteractive: true })
                return false;
            return _login is { IsInteractive: true } || Shell is { IsInteractive: true } && Shell.Pages.IsInteractive;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && CanHandleGlobalInput)
            Track(RequestQuitAsync());
    }

    private async Task RequestQuitAsync()
    {
        if (await AskAsync("退出星庭远征？\n档案将在下次启程时继续。"))
            await ShutdownAsync();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && CanHandleGlobalInput)
        {
            Track(BackAsync());
            GetViewport().SetInputAsHandled();
        }
    }

    public void Track(Task task) => _tasks.Add(task);
    public override void _Process(double delta)
    {
        foreach (var task in _tasks.ToArray())
        {
            if (!task.IsCompleted)
                continue;
            _tasks.Remove(task);
            task.GetAwaiter().GetResult();
        }

        if (Shell != null && Shell.State == NodeState.Ready && Shell.Pages.Current is LobbyView lobby)
            lobby.Tick(delta);
    }

    public void Gate(Action? skip)
    {
        SkipCinematic = skip;
        GetNode<Control>("CinematicGate").Visible = skip != null;
        if (skip != null)
            GetNode<Button>("CinematicGate/Skip").GrabFocus();
    }

    public void ApplySettings()
    {
        AudioServer.SetBusVolumeDb(0, Store.State.Volume == 0 ? -80 : Mathf.LinearToDb((float)Store.State.Volume / 100));
        if (!OS.GetCmdlineUserArgs().Contains("--lifetime-tests") && !OS.GetCmdlineUserArgs().Contains("--self-test") && !OS.GetCmdlineUserArgs().Contains("--capture-client") && !OS.GetCmdlineUserArgs().Contains("--verify-client-save"))
            DisplayServer.WindowSetMode(Store.State.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    public async Task CloseComparisonsAsync()
    {
        foreach (var node in Floating.Children.ToArray().Reverse())
            await Floating.CloseAsync(node);
    }

    public Task RewardAsync(string text, params RewardEntry[] rewards) => Modals.OpenAsync<RewardView>(new PresentationArgs(text, Rewards: rewards));
    public Task ModalAsync(ModalArgs args) => Modals.OpenAsync<ClientModalView>(args);
}
