# Unity Getting Started

本教程制作一个主菜单：打开设置、切换音量／画面分类、返回菜单、确认退出。

## 1. 准备项目

- 安装 Unity 6000.3.24f1 和 .NET 10 SDK。
- 创建一个 Unity 项目，将场景保存为 Assets/Scenes/Main.unity。
- Player Settings → Active Input Handling 选择 **Input Manager (Old)** 或 **Both**。本教程用旧 Input API 处理 Esc。

## 2. 安装框架

下载本仓库，在仓库根目录执行：

```powershell
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

此命令生成核心 DLL，不启动 Unity。然后在自己的 Unity 项目中：

1. 打开 Package Manager → **Add package from disk**。
2. 选择仓库中的 src/UIFramework.Unity/package.json。
3. 将 docs/examples/unity/TutorialHost.cs 复制到 Assets/Scripts。

包已声明 uGUI 依赖。本教程使用手写注册，无需 Source Generator。

## 3. 搭建场景

创建 Canvas 和 EventSystem，Canvas 下放两个挂载：

```text
Canvas
├── PagesMount
└── DialogsMount
EventSystem
```

| 对象 | 配置 |
| --- | --- |
| Canvas | Screen Space Overlay；保留 GraphicRaycaster |
| CanvasScaler | Scale With Screen Size；参考分辨率 1000×700 |
| PagesMount | RectTransform 四边 Stretch，偏移为 0；不放 Image |
| DialogsMount | 同上；在 Hierarchy 中位于 PagesMount 后面 |
| EventSystem | StandaloneInputModule；Submit=Return，Cancel=Escape |

挂载可通过创建 UI Panel 后移除 Image 得到。PagesMount 放主菜单，DialogsMount 放弹窗和框架生成的遮罩。

## 4. 创建三个 Prefab

资源保存到 **Assets/Resources/UI**。对象名称必须与下面一致；按钮文字可以自行修改。

所有 Prefab 根都只用 RectTransform，四边 Stretch、偏移为 0。Content 居中，约 440×380；控件放在 Content 内。

### Menu.prefab：主菜单

```text
Menu (RectTransform)
└── Content
    ├── Settings (Button)
    └── Quit (Button)
```

Settings 打开设置，Quit 打开退出确认。

### Settings.prefab：设置

```text
Settings (RectTransform)
└── Content (Image)
    ├── AudioTab (Button)
    ├── GraphicsTab (Button)
    ├── Close (Button)
    └── Body (RectTransform)
        ├── Audio (RectTransform)
        │   └── Volume (Slider)
        └── Graphics (RectTransform)
            └── Quality (Toggle)
```

Audio 与 Graphics 都保存在同一个 Prefab 中，填满 Body；运行时选择其中一页。把两个分类按钮放在上方，Close 放在底部。Volume 初始值可设 .65。

### Confirm.prefab：退出确认

```text
Confirm (RectTransform)
└── Content (Image)
    ├── Question (Text or TextMeshProUGUI)
    ├── Yes (Button)
    └── No (Button)
```

Question 显示“是否退出？”。Yes 确认，No 取消。

弹窗 Content 的 Image 接受射线；文字关闭 Raycast Target，按钮保留导航。资源根和挂载不放全屏 Image，遮罩由框架创建。

## 5. 接入框架

将 **TutorialHost** 挂在 Canvas 上，在 Inspector 赋值：

| 字段 | 拖入对象 |
| --- | --- |
| Canvas | 场景 Canvas |
| Events | 场景 EventSystem |
| PagesMount | PagesMount |
| DialogsMount | DialogsMount |

完整脚本已包含按钮订阅，Inspector 不再添加 OnClick。

初始化只做三件事：创建 Context 和容器、注册资源、显示主菜单。以下代码来自 TutorialHost，不需要额外复制：

```csharp
_ui = UnityUI.Create(Canvas, Events);
_pages = await _ui.Root.CreateContainerAsync<StackContainerNode>("Pages", PagesMount);
_dialogs = await _ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", DialogsMount);
_dialogs.ConfigureBackdrop(UnityBackdrops.Dim(Observe));

_ui.Register(() => new MenuView(OpenSettingsAsync, AskQuitAsync, Observe), "UI/Menu");
_ui.Register(() => new SettingsView(_dialogs, Observe), "UI/Settings");
_ui.Register<ConfirmView>("UI/Confirm");

await _pages.PushAsync<MenuView>();
```

业务流程对应这些 API：

| 操作 | 调用 |
| --- | --- |
| 显示主菜单 | `_pages.PushAsync<MenuView>()` |
| 打开设置 | `_dialogs.OpenAsync<SettingsView>()` |
| 切换分类 | `_tabs.SwitchAsync("Audio")` |
| 返回菜单 | `_dialogs.CloseAsync(settings)` |
| 等待退出答案 | `ShowDialogAsync<ConfirmView, bool>()` |
| 回答 | `Complete(result)` |

MenuView、SettingsView、ConfirmView 是普通 C# 节点，不挂到 GameObject。宿主 Update 观察异步任务；分类切换期间禁用分类与关闭按钮。

<details>
<summary>展开完整 TutorialHost.cs</summary>

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

</details>

## 6. 运行检查

进入 Play Mode，依次操作：

1. Settings → Graphics → Audio：音量值保留。
2. Back：回到主菜单。
3. Quit → Cancel：留在菜单。
4. Quit → Confirm：等待 Context 清理，然后调用 Application.Quit。

Esc 返回或取消，Enter 激活焦点按钮。音量／画质只演示控件状态。

Application.Quit 不会结束编辑器 Play Mode。实际项目在卸载场景前 await ShutdownAsync；手动 Stop 不属于正常关闭。

本教程已离线编译，尚未原生运行验证。适配状态见 [Unity 适配](../integrations/unity.md)。

[下一步](next-steps.md) · [Tab](../manual/tabs.md) · [结果弹窗](../manual/modals-and-dialogs.md)
