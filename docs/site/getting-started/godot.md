# Godot Getting Started

本教程从一个带标题和关闭按钮的窗口开始。完成后，点击场景中的按钮可以打开窗口；关闭后再打开会创建新实例。

## 1. 创建项目并引用框架

安装 Godot 4.7.2 .NET 和 .NET 10 SDK，创建 Godot .NET 项目。编辑项目的 `.csproj`，引用框架 Core 和 Godot 适配器；下面的相对路径适用于仓库内的 `docs/examples/godot`，在自己的项目中按实际位置调整：

```xml
<ItemGroup>
  <ProjectReference
    Include="../../../src/UIFramework.Core/UIFramework.Core.csproj" />
  <ProjectReference
    Include="../../../src/UIFramework.Godot/UIFramework.Godot.csproj" />
</ItemGroup>
```

在项目根目录新建 `HelloWindow.cs`、`TutorialHost.cs`。本仓库的 `docs/examples/godot` 提供完整场景作为对照；本教程会在编辑器中逐步创建 `HelloWindow.tscn` 和 `Main.tscn`。

## 2. 创建 HelloWindow 场景

按下面顺序创建场景：

1. 新建 Control 场景，保存为 `HelloWindow.tscn`。选中根 Control，将 Layout 设为 Full Rect，Mouse Filter 设为 Ignore。
2. 在根节点下添加 Panel，命名为 `Content`。Layout 选择 Center；Offsets 设为 Left=-210、Top=-120、Right=210、Bottom=120，得到 420×240 的面板。Mouse Filter 保持 Stop。
3. 在 Content 下添加 Label，命名为 `Title`；Text 设为 `Hello, UIFramework!`。设置矩形 Left=20、Top=28、Right=400、Bottom=84，水平与垂直对齐都设为 Center。
4. 在 Content 下添加 Button，命名为 `Close`，Text 设为 `Close`。设置矩形 Left=140、Top=154、Right=280、Bottom=198。

```text
HelloWindow (Control, Full Rect, Mouse Filter=Ignore)
└── Content (Panel, centered 420×240)
    ├── Title (Label: "Hello, UIFramework!")
    └── Close (Button)
```

`Content` 的鼠标过滤保持 Stop，使面板接收输入。标题和按钮使用相对路径 `Content/Title`、`Content/Close`，名字需与脚本一致。保存为 `HelloWindow.tscn`。

## 3. 编写窗口脚本

新建 `HelloWindow.cs`。`EngineUI.UINode` 继承 Control；`EngineUI` 是 `UIFramework.Godot` 的命名空间别名。构建后把脚本挂到 HelloWindow 场景根节点，将 Close 按钮拖入 Inspector 的 Close Button 字段。框架直接管理这个场景脚本，按钮使用原生引用。

```csharp
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using EngineUI = global::UIFramework.Godot;

public partial class HelloWindow : EngineUI.UINode
{
    [Export] public Button CloseButton { get; set; } = null!;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        CloseButton.Pressed += () => Observe(((PanelContainerNode)Parent!).CloseAsync(this));
        return Task.CompletedTask;
    }
}
```

## 4. 创建 Main 场景和宿主

新建 Control 场景并保存为 `Main.tscn`。在根节点下添加 `Windows` Control，Layout 设为 Full Rect、Mouse Filter 设为 Ignore。再添加 Button，命名为 `OpenWindow`；Layout 选择 Bottom Center，Offsets 设为 Left=-90、Top=-64、Right=90、Bottom=-20，Text 设为 `Open window`，Disabled 初始开启。

```text
Main (Control, TutorialHost.cs)
├── Windows (Control, Full Rect, Mouse Filter=Ignore)
└── OpenWindow (Button)
```

宿主创建 Context 和 Panel 容器，注册窗口场景，并在启动时打开窗口。`OpenAsync` 会复用当前单实例；关闭后再次点击会创建新实例。将以下完整代码保存为 `TutorialHost.cs`：

<details>
<summary>展开完整 TutorialHost.cs</summary>

```csharp
using EngineUI = global::UIFramework.Godot;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Godot;

public partial class TutorialHost : Control
{
    private readonly List<Task> _tasks = new();
    private Button _openWindowButton = null!;
    private PanelContainerNode _windows = null!;

    public UIContext UI { get; private set; } = null!;
    public Task Initialization { get; private set; } = null!;

    public override void _Ready()
    {
        Initialization = InitializeAsync();
        Observe(Initialization);
    }

    private async Task InitializeAsync()
    {
        _openWindowButton = GetNode<Button>("OpenWindow");
        UI = EngineUI.UI.Create(this);
        UI.BindLifetime(this);
        _windows = await UI.Root.CreateContainerAsync<PanelContainerNode>("Windows", GetNode<Control>("Windows"));
        UI.Register<HelloWindow>("res://HelloWindow.tscn");
        await _windows.OpenAsync<HelloWindow>();
        _openWindowButton.Pressed += OpenWindow;
        _openWindowButton.Disabled = false;
    }

    private void OpenWindow()
    {
        _openWindowButton.Disabled = true;
        Observe(OpenWindowAsync());
    }

    private async Task OpenWindowAsync()
    {
        await _windows.OpenAsync<HelloWindow>();
        _openWindowButton.Disabled = false;
    }

    private void Observe(Task task) => _tasks.Add(task);

    public override void _Process(double delta)
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

## 5. 挂载脚本并设置主场景

把 `TutorialHost.cs` 挂到 Main 根 Control 上；确认 HelloWindow 场景根节点已挂载 `HelloWindow.cs`，Close Button 已引用关闭按钮。确认节点名称与脚本中的 `GetNode` 路径一致，并在 Project Settings → Application → Run 中把 `Main.tscn` 设为主场景。

## 6. 运行并检查

运行项目后，窗口会先显示。点击 Close，窗口实例会销毁；再点 OpenWindow 会重新加载资源并创建新实例。窗口打开时重复调用 `OpenAsync` 会复用现有单实例。

宿主逐帧观察按钮触发的任务，失败时原始异常会继续抛出。Main 场景退出时，生命周期绑定会清理 Context。

[下一步](next-steps.md) · [窗口实例](../manual/panels.md) · [生命周期](../manual/lifecycle.md)
