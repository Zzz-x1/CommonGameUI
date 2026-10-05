# Godot Getting Started

## 场景：主菜单、设置与退出确认

使用 Godot 4.7.2 .NET 和 .NET 10 SDK。框架管理界面流程，按钮、Slider 和布局仍使用 Godot 原生控件。音量与画质控件在本教程只保持界面内的值，不修改实际音频或渲染设置。

## 安装与资源

最短路径：克隆或下载本仓库，从仓库根目录执行 `dotnet build docs/examples/godot/Tutorial.csproj`，在 Godot 打开 `docs/examples/godot/project.godot`。这是独立教程，不读写游戏 Demo 存档。

接入自己的项目时，在 `.csproj` 中添加 Core 和 Godot 两个 ProjectReference；将路径改为相对你项目文件的真实位置。本教程对应的引用如下：

```xml
<ProjectReference Include="../../../src/UIFramework.Core/UIFramework.Core.csproj" />
<ProjectReference Include="../../../src/UIFramework.Godot/UIFramework.Godot.csproj" />
```

资源全部是普通场景，位于教程项目根目录：

```text
Main (Control, TutorialHost.cs)
├── Pages (Control, Full Rect, Mouse Filter=Ignore)
└── Dialogs (Control, Full Rect, Mouse Filter=Ignore)
Menu.tscn: Control → Content → Settings, Quit (Button)
Settings.tscn: Control → Content (Panel)
  ├── AudioTab, GraphicsTab, Close (Button)
  └── Body (Control) → Audio/Volume (HSlider), Graphics/Quality (CheckButton)
Confirm.tscn: Control → Content (Panel) → Question (Label), Yes, No (Button)
```

场景根铺满挂载，Mouse Filter=Ignore；Content 是实际内容区域，弹窗 Content 使用 Stop。无需在弹窗资源中再放全屏遮罩。Tab 两页都保存在 Settings 场景中，由框架绑定与切换；首次默认选 Audio。按钮保留键盘焦点，滑动条和选项使用原生默认行为。完整资源随仓库提供，可在编辑器修改。

## 完整宿主与业务节点

将以下代码保存为 TutorialHost.cs，挂到 Main 根节点。View 是纯 C# 逻辑节点，不是 Godot 场景脚本。所有业务节点在同一文件中便于入门；实际项目可按模块拆分。

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

## 验证操作

运行 Main：设置→Graphics→Audio，Volume 值保留；Back 恢复主菜单。Quit→Cancel 保留主菜单；Quit→Confirm 等待 Context 清理后退出。Esc 先关闭设置或回答取消；主菜单 Esc 打开退出确认。Enter 激活当前焦点按钮。重开设置会创建新实例，所以尚未保存到模型的控件值重置。

`Observe` 收集任务，在 `_Process` 只对已完成任务取结果；失败直接传播原异常，没有 async void 或吞错。输入等待初始化与容器转场完成；Tab 选择按钮位于内容挂载外，因此用项目 Switching 状态同时禁用选择和关闭。已经选中的分类点击视为业务无变化。

正常退出先 await ShutdownAsync；不能在 `_ExitTree` 同步阻塞释放，强制停止运行也不等于正常关闭。技术性外部关闭未回答 Dialog 会取消等待，不等价于点击 Cancel 的 false。

按钮在 OnApplyState 中同步更新可用状态，早于框架恢复焦点；初始化期间控件尚未绑定，所以只在 Ready 状态更新。分类操作自己的 Switching 和答案标志即时更新，不通过每帧重复查询业务按钮。

## 继续阅读

[内嵌 Tab](../manual/tabs.md) · [生命周期](../manual/lifecycle.md) · [Godot 适配](../integrations/godot.md) · [下一步](next-steps.md)
