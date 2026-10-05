# Godot Getting Started

制作主菜单、设置分类与退出确认。仓库已提供独立教程工程，可直接运行或用于接入自己的项目。

## 1. 准备环境

安装 Godot 4.7.2 .NET 和 .NET 10 SDK。

## 2. 打开教程工程

从仓库根运行：

```powershell
dotnet build docs/examples/godot/Tutorial.csproj
```

用 Godot 打开 docs/examples/godot/project.godot。主场景是 Main.tscn，无玩家存档。

## 3. 接入自己的项目

在项目 .csproj 添加引用，按实际位置调整路径：

```xml
<ProjectReference Include="../../../src/UIFramework.Core/UIFramework.Core.csproj" />
<ProjectReference Include="../../../src/UIFramework.Godot/UIFramework.Godot.csproj" />
```

复制教程的 TutorialHost.cs、Main.tscn、Menu.tscn、Settings.tscn 和 Confirm.tscn 到项目根目录。下面代码使用这些固定资源路径。

## 4. 检查场景结构

```text
Main (Control, TutorialHost.cs)
├── Pages (Control)
└── Dialogs (Control)

Menu.tscn
└── Content → Settings, Quit (Button)

Settings.tscn
└── Content (Panel)
    ├── AudioTab, GraphicsTab, Close (Button)
    └── Body (Control)
        ├── Audio → Volume (HSlider)
        └── Graphics → Quality (CheckButton)

Confirm.tscn
└── Content (Panel) → Question (Label), Yes, No (Button)
```

- 根与空挂载：Full Rect、Mouse Filter=Ignore。
- 弹窗 Content：实际面板区域，Mouse Filter=Stop；遮罩由框架创建。
- Tab 两页都保存在 Settings.tscn，运行时只切换显示。
- 按钮保留键盘焦点，对象名称与代码路径一致。

## 5. 接入逻辑

TutorialHost.cs 挂在 Main 根节点。MenuView、SettingsView、ConfirmView 是普通 C# 节点，不挂到 Godot 场景。

宿主先创建容器，再注册资源、打开菜单。以下片段已经包含在完整源码中：

```csharp
UI = GodotUI.Create(this);
Pages = await UI.Root.CreateContainerAsync<StackContainerNode>("Pages", GetNode<Control>("Pages"));
Dialogs = await UI.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", GetNode<Control>("Dialogs"));
Dialogs.ConfigureBackdrop(GodotBackdrops.Dim(Observe));

UI.Register(() => new MenuView(OpenSettingsAsync, AskQuitAsync, Observe), "res://Menu.tscn");
UI.Register(() => new SettingsView(Dialogs, Observe), "res://Settings.tscn");
UI.Register<ConfirmView>("res://Confirm.tscn");
await Pages.PushAsync<MenuView>();
```

按钮任务交给 Observe，由 _Process 传播异常；分类切换时同时禁用选择与关闭按钮。

<details>
<summary>展开完整 TutorialHost.cs</summary>

```csharp
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
```

</details>

## 6. 运行检查

1. Settings → Graphics → Audio：音量值保留。
2. Back 返回菜单，恢复键盘焦点。
3. Quit → Cancel 留在菜单；Confirm 清理 Context 后退出。

Esc 返回或取消，Enter 激活焦点按钮。正常释放场景前 await ShutdownAsync。音量／画质只演示控件值。

[下一步](next-steps.md) · [Tab](../manual/tabs.md) · [生命周期](../manual/lifecycle.md)
