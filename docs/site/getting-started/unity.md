# Unity Getting Started

本教程从一个带标题和关闭按钮的窗口开始。完成后，点击场景中的按钮可以打开窗口；关闭后再打开会创建新实例。

## 1. 准备项目与框架

安装 Unity 6000.3.24f1 和 .NET 10 SDK，创建项目和 `Main` 场景。在场景中用 **GameObject → UI → Canvas** 创建 Canvas；保留自动添加的 CanvasScaler 和 GraphicRaycaster。下载本仓库，在仓库根目录准备本地 Unity 包：

```powershell
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

然后在 Unity 的 Package Manager 中选择 **Add package from disk**，打开仓库里的 `src/UIFramework.Unity/package.json`。该包会一并引用 Core 和 uGUI。

## 2. 创建窗口 Prefab

按这个顺序制作 Prefab：

1. 在 Hierarchy 选中 Canvas，右键选择 **UI → Panel**，将新对象命名为 `HelloWindow`。
2. 选中根 RectTransform，在 Anchor Presets 中选四边 Stretch；Left、Top、Right、Bottom 都设为 0。移除根节点的 Image，避免全屏根节点挡住场景输入。
3. 在 `HelloWindow` 下创建 **UI → Panel**，命名为 `Content`。RectTransform 设为中心锚点、Pos X/Y 为 0、Width 为 420、Height 为 240。保留它的 Image。
4. 在 Content 下创建 **UI → Legacy → Text**，命名为 `Title`；将 RectTransform 锚点设为 Center，Width=360、Height=56、Pos X=0、Pos Y=45。Text 设为 `Hello, UIFramework!`，水平和垂直对齐居中。
5. 在 Content 下创建 **UI → Button**，命名为 `Close`；将 RectTransform 锚点设为 Center，Width=140、Height=44、Pos X=0、Pos Y=-65。将其子 Text 改为 `Close`。

```text
HelloWindow (RectTransform, Stretch)
└── Content (Image, centered 420×240)
    ├── Title (Text: "Hello, UIFramework!")
    └── Close (Button)
```

将 `HelloWindow` 拖到 `Assets/Resources/UI/HelloWindow.prefab`，再从场景中删除临时实例。资源根没有 Image，实际面板 `Content` 接收鼠标输入。

## 3. 编写窗口脚本

新建 `HelloWindow.cs`。这个类是框架的业务节点，由注册工厂创建；它不是 MonoBehaviour，不挂到 Prefab 上。它按资源路径找到关闭按钮，并让所属容器关闭当前节点。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UnityEngine.UI;

public sealed class HelloWindow : UINode
{
    private readonly PanelContainerNode _windows;
    private readonly Action<Task> _observe;

    public HelloWindow(PanelContainerNode windows, Action<Task> observe)
    {
        _windows = windows;
        _observe = observe;
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        var closeButton = this.GetComponent<Button>("Content/Close");
        closeButton.onClick.AddListener(() => _observe(_windows.CloseAsync(this)));
        return Task.CompletedTask;
    }
}
```

## 4. 搭建 Main 场景并编写宿主

从刚保存的 Prefab 删除场景中的临时 `HelloWindow`。在 Canvas 下创建 Empty UI Object，命名为 `WindowsMount`；RectTransform 设为四边 Stretch、偏移均为 0，不添加 Image。然后添加一个 Button，命名为 `OpenWindowButton`；设为底部中央锚点、Width 180、Height 44、Pos Y 为 40，文字设为 `Open Window`。

Canvas 使用 Screen Space - Overlay。CanvasScaler 设为 **Scale With Screen Size**，参考分辨率 `1000 × 700`。场景需要一个 EventSystem；创建 Button 时若 Unity 已自动创建，就复用现有对象。

```text
Canvas (Canvas, CanvasScaler, TutorialHost)
├── WindowsMount (RectTransform, Stretch, no Image)
└── OpenWindowButton (Button)
EventSystem
```

宿主创建 Context 和 Panel 容器、注册 Prefab，并在启动时打开窗口。再次点击时 `OpenAsync` 复用当前单实例；关闭后再点会创建新的实例。完整脚本可复制到 `TutorialHost.cs`：

<details>
<summary>展开完整 TutorialHost.cs</summary>

```csharp
using System.Collections.Generic;
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
    public RectTransform WindowsMount;
    public Button OpenWindowButton;

    private readonly List<Task> _tasks = new List<Task>();
    private UIContext _ui;
    private PanelContainerNode _windows;

    private void Start() => Observe(InitializeAsync());

    private async Task InitializeAsync()
    {
        _ui = UnityUI.Create(Canvas, Events);
        _ui.BindLifetime(gameObject);
        _windows = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", WindowsMount);
        _ui.Register(() => new HelloWindow(_windows, Observe), "UI/HelloWindow");
        await _windows.OpenAsync<HelloWindow>();
        OpenWindowButton.onClick.AddListener(OpenWindow);
        OpenWindowButton.interactable = true;
    }

    private void OpenWindow()
    {
        OpenWindowButton.interactable = false;
        Observe(OpenWindowAsync());
    }

    private async Task OpenWindowAsync()
    {
        await _windows.OpenAsync<HelloWindow>();
        OpenWindowButton.interactable = true;
    }

    private void Observe(Task task) => _tasks.Add(task);

    private void Update()
    {
        for (var i = _tasks.Count - 1; i >= 0; i--)
        {
            var task = _tasks[i];
            if (!task.IsCompleted) continue;
            _tasks.RemoveAt(i);
            task.GetAwaiter().GetResult();
        }
    }
}
```

</details>

## 5. 挂载脚本并连接场景资源

把 `TutorialHost.cs` 挂到 Canvas。Inspector 中设置以下引用：

| 字段 | 对象 |
| --- | --- |
| Canvas | 场景 Canvas |
| Events | 场景 EventSystem |
| WindowsMount | Canvas 下的 WindowsMount |
| OpenWindowButton | Canvas 下的 OpenWindowButton |

将 OpenWindowButton 的 Interactable 初始设为关闭。窗口和宿主脚本都放在 `Assets` 中；`HelloWindow` 类不挂组件，容器根据 `Register` 的资源路径加载 Prefab。

## 6. 运行并检查

进入 Play Mode，启动时应看到窗口。点击 Close 后窗口销毁；点击 Open Window 会重新创建。窗口打开时反复点击 Open Window 不会叠出重复窗口。

所有按钮操作都会观察异步任务；出错时 Unity Console 会收到原始异常。场景中的 Canvas 销毁时，框架会清理对应 Context。

[下一步](next-steps.md) · [窗口实例](../manual/panels.md) · [生命周期](../manual/lifecycle.md)
