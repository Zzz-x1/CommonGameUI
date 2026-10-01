# UIFramework

纯 C#、渲染无关的层级 UI 管理框架 V2，附持续维护的 Godot C# Demo。

## 工程结构

| 路径 | 内容 |
| --- | --- |
| `src/UIFramework.Core` | netstandard2.1 管理内核、默认 Root、节点与容器 |
| `src/UIFramework.Godot` | Godot Control/PackedScene 适配器与淡入淡出转场 |
| `src/UIFramework.Generators` | 注册与类型化调用的 Source Generator，不包含自定义 Analyzer |
| `Demo/Godot` | Godot 4.7.2 .NET / net10.0 示例项目 |
| `tests/UIFramework.Tests` | 核心行为与生成结果编译测试 |

设计依据：[设计文档](docs/design-decisions.md)、[V1 开发文档](docs/v1-development.md)、[V2 开发计划](docs/v2-development.md)。V2 已补全生命周期、状态通知与处理器组合；Demo 使用虚构冒险游戏的纯 UI 场景。

## 运行

需要 .NET 10 SDK 和 Godot 4.7.2 .NET。下载的便携引擎放在 `.tools`，不进入项目源码，也不修改系统安装。

```powershell
# 没有引擎时获取固定版本。
./scripts/Get-Godot.ps1

# 启动可交互 Demo。
./scripts/Run-Demo.ps1

# 核心测试及 Godot 自动场景验证。
./scripts/Test.ps1

# 也可使用自己的相同版本引擎。
./scripts/Run-Demo.ps1 -GodotPath 'C:/Tools/Godot/Godot_console.exe'
```

或者用 Godot .NET 编辑器导入 `Demo/Godot/project.godot`，构建 C# 后运行主场景。纯核心测试不需要 Godot：

```powershell
dotnet test tests/UIFramework.Tests/UIFramework.Tests.csproj
```

## API

```csharp
var ui = new UIContext(host, resourceLoader, logger);
var root = ui.Root; // 默认只包含结构性 Root。

var stack = new StackContainerNode();
await stack.AttachAsync(root, "ProjectPages", mount);
ui.Register<InventoryView>("project-specific-resource");
await stack.PushAsync<InventoryView>();
await stack.PopAsync();

var tabs = new TabContainerNode();
await tabs.AttachAsync(root, "ProjectTabs", embeddedMount);
await tabs.BindAsync("Graphics", new UINode(), graphicsDisplay);
await tabs.BindAsync("Audio", new UINode(), audioDisplay);
await tabs.SwitchAsync("Graphics");
await tabs.SwitchAsync("Audio");
```

示例中的 `mount` / `embeddedMount` 是后端显示句柄，不是框架默认节点。内嵌 Tab 不加载或克隆场景，不要求业务 View 类。未知名称、重复项、重复切换当前项均明确抛错；UI 可以将当前标签按钮禁用。

`PanelContainerNode.OpenAsync<T>` 支持独立实例，`CloseAsync(node)` 关闭具体对象；`ModalContainerNode.OpenAsync<T>` / `CloseAsync(node)` 处理模态窗口。`ShowDialogAsync<T, TResult>` 等待 `DialogNode<TResult>.Complete(result)`，关闭成功后返回结果。

业务类可以使用 View 命名。声明 `[UIResource("res://...")]` 后，引用生成器并 `using UIFramework.Generated`，可调用 `ui.RegisterGeneratedUI()` 和生成的 `PushInventoryViewAsync` 等方法。`UINode<TArgs>` 的生成调用接受对应参数类型；生成代码仍调用同一核心 API。自定义构造通过 `ui.Register(() => new MyNode(service), resource)` 手写接入。

## Godot 初始化

Godot 接入可直接使用适配器便利 API：

```csharp
using UIFramework.Godot;

var ui = GodotUI.Create(this); // 在 Godot 场景线程、进入场景树后调用。
ui.RegisterGeneratedUI(); // 项目生成入口，需要 using UIFramework.Generated。
var stack = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pagesControl);
await stack.PushInventoryViewAsync();

var tabs = await ui.Root.CreateContainerAsync<TabContainerNode>("Tabs", tabsControl);
await tabs.BindAsync("Graphics", graphicsControl); // 基础 Node，无需包装显示句柄。
await tabs.BindAsync("Audio", audioControl);
await tabs.SwitchAsync("Graphics");
await ui.DisposeAsync(); // 场景退出时显式销毁上下文。
```

`GodotUI.Create(this, loader: customLoader, logger: customLogger)` 支持替换服务。已有业务节点使用 `await node.AttachToAsync(parent, name, control)`；业务代码用 `this.GetControl<Button>("Path")` 获取控件。便利挂载与 Tab 绑定借用已有 Control，不负责释放它；项目新建且交给框架销毁的 Control 使用底层 `GodotUIElement(control, owned: true)`。上下文创建不会自动添加项目容器，异步任务仍需调用方观察。

纯构造、注册、查询和配置保持同步。`AttachToAsync` 与 Control 版 `BindAsync` 直接转发核心任务；它们仍可等待，因为挂载会执行可能异步的初始化及失败清理。`AttachToAsync` 返回 `Task`，已有节点直接使用原引用。容器创建需要等初始化成功后返回新实例。不要用同步阻塞或 fire-and-forget 绕过这些等待。

## 生命周期与失败规则

V2 的 `OnEnteringAsync/OnExitingAsync` 承担可等待准备，`OnEntered/OnExited` 是提交后的同步通知。`OnActivated/OnDeactivated/OnVisibilityChanged` 表达有效状态变化；`IsActive` 不含临时输入锁，`IsInteractive` 包含。上下文提供原因、源/目标和首次进入标志。独立行为可通过 `AddLifecycle(new MyBehavior())` 在挂载前注册，继承 `NodeLifecycle` 只覆写需要的阶段。完整契约集中于设计文档第 7 节。

旧 `OnEnterAsync/OnExitAsync` 已删除，工程内调用已迁移；没有兼容入口。完成通知失败保持已提交状态并传播原异常，关闭仍执行必要清理。自定义转场实现同步 `IUITransition.Restore` 恢复取消/失败时修改的表现状态，恢复失败也会暴露。

- 初始化一次；进入/退出可重复；隐藏保留实例，销毁结束子树。
- 栈保留覆盖项，出栈销毁；Tab 保留所有绑定项；独立面板按实例关闭。
- 同容器并发或重入直接抛错，未引入操作队列。
- 所有异步操作可等待；节点销毁取消其操作并等待清理。
- 原始异常记录一次并向上传播，不包装为统一异常，不吞错、不降级。
- 清理同样失败时，原始主异常仍抛出；附加原异常可通过 `UIFailure.GetSecondaryExceptions(ex)` 获取，并单独打印日志。
- 实际后端 API 必须在宿主 UI 线程调用；Godot 会明确校验。初版不自动把后台调用调度到场景线程。

Godot 显示句柄分 owned（动态资源根）与 borrowed（内嵌控件）。借用句柄销毁仅结束逻辑管理，不单独释放父场景资源。

## 当前边界

当前实际验证了 Godot 桌面端。其他引擎需要适配器，尚未验证。Godot 场景加载采用同步 `ResourceLoader.Load`，包装在可等待接口中；大资源后台加载、编辑器节点配置、运行时重挂载、通用缓存/池化、绑定框架及诊断分析均未实现。

素材来源与许可见 [Demo 资源记录](Demo/Godot/Assets/SOURCES.md)。
