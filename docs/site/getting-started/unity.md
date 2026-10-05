# Unity Getting Started

## 场景与状态

本教程完成与 Godot 相同的主菜单、设置分类、确认与返回。当前适配源码以 Unity 6000.3.24f1、uGUI 2.0.0 验证编译。最新适配修复与本教程仅离线编译，未完成原生运行验证；游戏 Unity Demo 的复杂时间轴资源还有待验证项。不要把这理解为已验证全部 Unity 平台。

## 安装

下载本仓库，安装 .NET 10 SDK。从仓库根目录执行 `./scripts/Run-UnityDemo.ps1 -PrepareOnly`，只构建核心 DLL 到本地包，不启动编辑器。自己的项目在 Package Manager 使用 Add package from disk，选择 `src/UIFramework.Unity/package.json`。包依赖 uGUI 和 Animation 模块。核心 DLL 为构建产物，不纳入源码；首次接入必须先准备。

本教程不使用 Source Generator，不需要 Unity CLI。将 `docs/examples/unity/TutorialHost.cs` 复制到自己的 Assets/Scripts。项目 Active Input Handling 选 Input Manager (Old) 或 Both；以下 Esc 使用旧 Input API。EventSystem 使用 StandaloneInputModule，Submit 为 Return，Cancel 为 Escape；使用新输入系统时自行替换宿主按键与输入模块。

## 资源与 Inspector

```text
Scene
├── Canvas (Screen Space Overlay, CanvasScaler, GraphicRaycaster, TutorialHost)
│   ├── PagesMount (RectTransform, Stretch)
│   └── DialogsMount (RectTransform, Stretch, 位于 PagesMount 后)
└── EventSystem (StandaloneInputModule)
Assets/Resources/UI/Menu.prefab: RectTransform → Content → Settings, Quit
Assets/Resources/UI/Settings.prefab: RectTransform → Content (Image)
  ├── AudioTab, GraphicsTab, Close (Button)
  └── Body → Audio/Volume (Slider), Graphics/Quality (Toggle)
Assets/Resources/UI/Confirm.prefab: RectTransform → Content (Image)
  └── Question (Text), Yes, No (Button)
```

CanvasScaler 采用 Scale With Screen Size、1000×700。所有 Prefab 根为 RectTransform，Stretch 填满挂载；Content 居中约 440×380。分类 Body 下两个页面都存进 Prefab，重叠显示区域；由运行时绑定默认 Audio。按钮可导航，标签的 Raycast Target 关闭，Content Image 和 Button 的 Graphic 接受射线。Volume 起始值设 .65；Quality 为普通 Toggle，值只用于 UI 演示。

PagesMount／DialogsMount 与 Prefab 根不放可命中的全屏 Image；框架自动创建遮罩。将 Canvas、Events、PagesMount、DialogsMount 拖到 TutorialHost 对应字段。不要在 Inspector 再绑定同一个按钮事件，代码已经订阅。

## 完整宿主与节点

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TutorialHost : MonoBehaviour
{
    public Canvas Canvas;
    public EventSystem Events;
    public RectTransform PagesMount, DialogsMount;
    private readonly List<Task> _tasks = new List<Task>();
    private UIContext _ui;
    private StackContainerNode _pages;
    private ModalContainerNode _dialogs;
    private Task _ready;
    private bool _stopping;
    private bool _shutdownComplete;
    private void Start() { _ready = InitializeAsync(); Observe(_ready); }
    private async Task InitializeAsync()
    {
        _ui = UnityUI.Create(Canvas, Events);
        _pages = await _ui.Root.CreateContainerAsync<StackContainerNode>("Pages", PagesMount);
        _dialogs = await _ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", DialogsMount);
        _dialogs.ConfigureBackdrop(UnityBackdrops.Dim(Observe));
        _ui.Register(() => new MenuView(OpenSettingsAsync, AskQuitAsync, Observe), "UI/Menu");
        _ui.Register(() => new SettingsView(_dialogs, Observe), "UI/Settings");
        _ui.Register<ConfirmView>("UI/Confirm");
        await _pages.PushAsync<MenuView>();
    }
    private void Observe(Task task) { _tasks.Add(task); }
    private void Update()
    {
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            var task = _tasks[i];
            if (!task.IsCompleted) continue;
            _tasks.RemoveAt(i);
            task.GetAwaiter().GetResult();
        }
        if (_stopping || _ready == null || !_ready.IsCompletedSuccessfully || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (_dialogs.Current is ConfirmView confirm && confirm.IsInteractive) confirm.Answer(false);
        else if (_dialogs.Current is SettingsView settings && settings.IsInteractive && !settings.Switching) Observe(_dialogs.CloseAsync(settings));
        else if (_dialogs.Current == null && _pages.IsInteractive) Observe(AskQuitAsync());
    }
    private Task OpenSettingsAsync() => _dialogs.OpenAsync<SettingsView>();
    private async Task AskQuitAsync()
    { if (await _dialogs.ShowDialogAsync<ConfirmView, bool>()) await ShutdownAsync(); }
    public async Task ShutdownAsync()
    {
        _stopping = true;
        await _ui.DisposeAsync();
        _shutdownComplete = true;
        Application.Quit();
    }
    private bool OnApplicationWantsToQuit()
    {
        if (_stopping) return _shutdownComplete;
        if (_ready != null && _ready.IsCompletedSuccessfully && _dialogs.Current == null && _pages.IsInteractive) Observe(AskQuitAsync());
        return false;
    }
    private void OnEnable() { Application.wantsToQuit += OnApplicationWantsToQuit; }
    private void OnDisable() { Application.wantsToQuit -= OnApplicationWantsToQuit; }
}

public sealed class MenuView : UINode
{
    private readonly Func<Task> _settings, _quit;
    private readonly Action<Task> _observe;
    private Button _settingsButton, _quitButton;
    public MenuView(Func<Task> settings, Func<Task> quit, Action<Task> observe)
    { _settings = settings; _quit = quit; _observe = observe; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _settingsButton = this.GetComponent<Button>("Content/Settings"); _quitButton = this.GetComponent<Button>("Content/Quit");
        _settingsButton.onClick.AddListener(() => _observe(_settings()));
        _quitButton.onClick.AddListener(() => _observe(_quit()));
        return Task.CompletedTask;
    }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput() { _settingsButton.interactable = IsInteractive; _quitButton.interactable = IsInteractive; }
}

public sealed class SettingsView : UINode
{
    private readonly ModalContainerNode _dialogs;
    private readonly Action<Task> _observe;
    private TabContainerNode _tabs;
    private Button _audio, _graphics, _close;
    public bool Switching { get; private set; }
    public SettingsView(ModalContainerNode dialogs, Action<Task> observe) { _dialogs = dialogs; _observe = observe; }
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        _audio = this.GetComponent<Button>("Content/AudioTab"); _graphics = this.GetComponent<Button>("Content/GraphicsTab");
        _close = this.GetComponent<Button>("Content/Close");
        _tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", (RectTransform)this.GetRect().Find("Content/Body"), token);
        await _tabs.BindAsync("Audio", (RectTransform)this.GetRect().Find("Content/Body/Audio"), token);
        await _tabs.BindAsync("Graphics", (RectTransform)this.GetRect().Find("Content/Body/Graphics"), token);
        await _tabs.SwitchAsync("Audio", playAnimation: false, cancellationToken: token);
        _audio.onClick.AddListener(() => _observe(SwitchAsync("Audio")));
        _graphics.onClick.AddListener(() => _observe(SwitchAsync("Graphics")));
        _close.onClick.AddListener(() => _observe(_dialogs.CloseAsync(this)));
    }
    public async Task SwitchAsync(string name)
    {
        Switching = true; RefreshInput();
        try { if (_tabs.CurrentName != name) await _tabs.SwitchAsync(name); }
        finally { Switching = false; if (State == NodeState.Ready) RefreshInput(); }
    }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput()
    { bool enabled = IsInteractive && !Switching; _audio.interactable = enabled; _graphics.interactable = enabled; _close.interactable = enabled; }
}

public sealed class ConfirmView : DialogNode<bool>
{
    private Button _yes, _no;
    private bool _answered;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _yes = this.GetComponent<Button>("Content/Yes"); _no = this.GetComponent<Button>("Content/No");
        _yes.onClick.AddListener(() => Answer(true)); _no.onClick.AddListener(() => Answer(false));
        return Task.CompletedTask;
    }
    public void Answer(bool value) { Complete(value); _answered = true; RefreshInput(); }
    protected override void OnApplyState() { if (State == NodeState.Ready) RefreshInput(); }
    public void RefreshInput() { _yes.interactable = IsInteractive && !_answered; _no.interactable = IsInteractive && !_answered; }
}
```

## 验证流程与关闭

Settings→Graphics→Audio 保留 Slider；Back 回主菜单；Quit→Cancel 不退出；Quit→Confirm 先清理再请求退出。Esc 优先当前弹窗；Enter 提交当前 EventSystem 焦点。编辑器中的 Application.Quit 不结束 Play Mode，手动 Stop 不属于可等待的正常关闭；卸载场景前由项目 await ShutdownAsync 并按项目需求替换退出动作。不要在 OnDestroy 中 async void 清理。

宿主 Update 观察任务并原样传播错误。分类选择和关闭共用项目 Switching 状态，容器仍管理自身输入锁。外部关闭未回答的结果弹窗会取消，不转换为 false。尚未验证本教程原生运行；源代码和 API 引用经过离线编译。

按钮在 OnApplyState 中同步更新可用状态，早于框架恢复焦点；初始化期间控件尚未绑定，所以只在 Ready 状态更新。分类操作自己的 Switching 和答案标志即时更新，不通过每帧重复查询业务按钮。

## 继续阅读

[Unity 适配](../integrations/unity.md) · [结果弹窗](../manual/modals-and-dialogs.md) · [输入与焦点](../manual/input-and-focus.md) · [下一步](next-steps.md)
