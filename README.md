# UIFramework

纯 C#、渲染无关的层级 UI 管理框架，附持续维护的 Godot C# 游戏客户端 Demo「星庭远征」。

## 工程结构

| 路径 | 内容 |
| --- | --- |
| `src/UIFramework.Core` | netstandard2.1 管理内核、默认 Root、节点与容器 |
| `src/UIFramework.Godot` | Godot Control/PackedScene 适配器、转场预设、AnimationPlayer 接入与模态遮罩 |
| `src/UIFramework.Generators` | 注册与类型化调用的 Source Generator，不包含自定义 Analyzer |
| `Demo/Godot` | Godot 4.7.2 .NET / net10.0 示例项目 |
| `tests/UIFramework.Tests` | 核心行为与生成结果编译测试 |

设计依据：[设计文档](docs/design-decisions.md)、[V1 开发文档](docs/v1-development.md)、[V2 开发计划](docs/v2-development.md)。V3 在 V2 生命周期上加入单实例窗口、隐藏恢复和返回指定历史实例；Demo 使用虚构冒险游戏的纯 UI 场景。V3 实际契约与验收集中于设计文档第 8.9、8.10 节。

V4 增加转场配置、独立执行状态、源/目标并行动画及共享/逐层模态遮罩，实际契约见设计文档第 9.5、9.6 节。没有旧转场接口兼容层；资源预加载、Esc 返回与页面焦点扩展未纳入本版。

V5 增加逐次 playAnimation 开关与统一的 Transitions.Select 包装函数，阶段及来源条件由普通 C# 表达；实际契约与验收见设计文档第 10.6、10.7 节。

后续对照 USN 补齐转场绘制优先级、延迟/缓动/起止帧参数及运行状态/进度通知，实际契约与验收集中于设计文档第 11 节。

资源取得与实例创建已分离，Godot 使用后台加载。预加载、共享持有、取消和释放的实际契约见设计文档第 10.4 节，验收见第 12 节。

当前 Demo 已完整替换为原创中文奇幻客户端，包含角色、背包、编队、商店、任务、邮件、活动和设置等本地 UI 闭环，使用 AnimationPlayer 制作复杂过场。操作与场景说明见 [Demo README](Demo/Godot/README.md)，实际范围与验收见设计文档第 13 节。

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

`PanelContainerNode.OpenAsync<T>` 默认取得或创建容器内单实例，隐藏项自动恢复，显示项返回原对象而不重播生命周期。多实例在注册时明确指定 `WindowInstanceMode.Multiple`；该设置只影响 Panel，不影响 Stack/Modal。`HideAsync` 保留实例，`ShowAsync` 恢复，`CloseAsync` 最终销毁。`TryGet<T>(out node)` 同步查询包含隐藏项的已提交窗口；多个匹配实例直接报歧义错误。

```csharp
ui.Register<CardView>("card-resource", windowMode: WindowInstanceMode.Multiple);
var windows = new PanelContainerNode();
await windows.AttachAsync(ui.Root, "ProjectWindows", windowMount);
var inventory = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(inventory);
await windows.OpenAsync<InventoryView>(); // 显示同一个实例。
await windows.HideAsync(inventory);
await windows.ShowAsync(inventory);
await windows.CloseAsync(inventory); // 下次打开创建新实例。

var first = await stack.PushAsync<InventoryView>();
await stack.PushAsync<ItemDetailsView>();
await stack.PushAsync<ItemNotesView>();
await stack.PopToAsync(first); // 跳过中间页，恢复 first，等待移除项清理。
```

窗口参数是创建参数：新建使用 `OpenAsync<T>(args)`，已有参数化单实例重开省略 args 或传 null，更新数据调用业务方法。已有单实例再次带非 null 参数 Open、Hide 已隐藏对象、Show 已显示对象均报错。`PopToAsync` 仅接收当前栈中更早的实例，不能返回当前项；中间隐藏项不重播显示生命周期。

`ModalContainerNode.OpenAsync<T>` / `CloseAsync(node)` 处理模态窗口。`CloseToAsync(target)` 一次返回具体下层实例，不逐层激活中间弹窗。`ShowDialogAsync<T, TResult>` 等待 `DialogNode<TResult>.Complete(result)`，关闭成功后返回结果；外部关闭未回答的弹窗以取消结束，已回答的弹窗等待关闭清理后返回结果，不重复关闭。

```csharp
await modals.CloseToAsync(shop); // 保留原商店，清理其上的详情和确认。
await tabs.HideAsync();         // 清空当前选中，保留全部内嵌页面。
await tabs.SwitchAsync("Map"); // 再次展开原实例。
```

批量返回仅当前顶层执行退出生命周期和动画，中间项暂时隐藏、提交后销毁；目标恢复输入与原焦点。Tab 收起执行当前项的退出生命周期，保留控件值；无当前项或无效返回目标直接报错。提交前失败恢复原状态，提交后通知/清理失败保留已提交状态并传播原异常。实际契约及验收见设计文档第 10.4、12.2 节。

业务类可以使用 View 命名。声明 `[UIResource("res://...")]` 后，引用生成器并 `using UIFramework.Generated`，可调用 `ui.RegisterGeneratedUI()` 和生成的 `PushInventoryViewAsync` 等方法。属性可设置 `WindowMode = WindowInstanceMode.Multiple`。`UINode<TArgs>` 的窗口调用同时生成带参创建、无参重开重载；生成代码仍调用同一核心 API。自定义构造通过 `ui.Register(() => new MyNode(service), resource)` 手写接入。

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

旧 `OnEnterAsync/OnExitAsync` 已删除，工程内调用已迁移；没有兼容入口。完成通知失败保持已提交状态并传播原异常，关闭仍执行必要清理。自定义转场通过 `IUITransition.Create(context)` 创建一次 `IUITransitionRun`，用 `PlayAsync(token)` 播放、同步 `Restore()` 恢复临时表现并清理动画。成功和失败均恢复，恢复失败同样暴露。

## V4 转场与遮罩

```csharp
// Godot 配置；其他引擎提供自己的动画实现。
ui.DefaultTransition = GodotTransitions.Fade(0.15);
stack.ChildTransition = GodotTransitions.SlideHorizontal(0.20);
await stack.PushAsync<ItemDetailsView>(); // 源向左退出，目标从右进入。
await stack.PopAsync();                  // 反向播放并恢复原页面。
```

节点 `Transition` 覆盖直接管理容器的 `ChildTransition`，再继承 `ui.DefaultTransition`；默认 Instant。配置不穿透到后代容器，不修改业务操作 API。单界面可在构造/初始化中设置 `Transition = GodotTransitions.ScaleFade(targetPath: "Center")`。滑动/缩放目标不能直接受 Godot 布局 Container 控制，应提供内容包装层；未知路径直接报错。Fade 保留原 tint/alpha，滑动/缩放恢复原位置、缩放和 pivot。

```csharp
var modals = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", modalMount);
modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared);
// 每层都加深背景时明确改用 ModalBackdropMode.PerModal。
```

`Track(Task)` 是项目保存并观察异步任务的入口（Demo 示范于 DemoApp），必须观察失败；遮罩被释放后也不能丢弃该任务。弹窗资源仅提供内容，根/外部布局节点允许鼠标穿透，内容面板拦截鼠标；不要再内嵌全屏遮罩。

普通窗口可设置 `DismissRequested = ct => modals.CloseAsync(this, cancellationToken: ct)`；结果弹窗可设置为 `Complete(false)` 后返回 `Task.CompletedTask`。未设置时外部点击正常不关闭，`RequestDismissAsync` 返回 false；缺失窗口、操作中请求或重复并发请求直接报错。遮罩创建、动画和清理失败均原样传播。模态操作期间临时阻挡全上下文输入，提交时才改变稳定激活状态；不同模态容器同时取得输入仍不支持，会明确报错。

## V5 按次动画开关与上下文选择

```csharp
await tabs.SwitchAsync("Graphics", playAnimation: false); // 初始显示，生命周期照常执行。
await stack.PopToAsync(inventory, playAnimation: false);  // 即时返回，仍等待清理。

var fade = GodotTransitions.Fade();
var slide = GodotTransitions.SlideHorizontal();
stack.ChildTransition = Transitions.Select(context => context.Phase switch
{
    TransitionPhase.PushEnter or TransitionPhase.PopExit => slide,
    _ => fade
});
```

固定配方直接赋值。动态选择只使用 `Transitions.Select(Func<TransitionContext, IUITransition>)`，可按 Phase、Source/Target 的类型或名称、业务状态分支。选择函数同步且只读；进入/退出准备前解析一次，准备后创建执行对象。函数抛错、返回 null 或循环选择均记录并传播错误，不改用其他配方。

Panel Open/Show/Hide/Close、Stack Push/Pop/PopTo、Tab Switch、Modal Open/Close/ShowDialog 均支持默认 true 的 playAnimation，生成的类型化 API 同步透传。false 跳过选择和动画运行，遮罩即时显隐，仍等待资源、生命周期和清理；ShowDialog 的开场及关闭使用同一开关。业务参数与 CancellationToken 建议按名传入，取消参数名称为 cancellationToken。

## 动画参数、顺序与运行信息

```csharp
var neutral = new GodotTransitionFrame();
var enter = GodotTransitions.Simple(
    neutral with { Offset = new Vector2(0, 16), Scale = Vector2.One * .96f, Alpha = 0 },
    neutral, duration: .20, delay: .03, targetPath: "Center",
    transition: Tween.TransitionType.Quart, ease: Tween.EaseType.Out);
var exit = GodotTransitions.Simple(neutral, neutral with { Alpha = 0 }, duration: .12, targetPath: "Center");
node.Transition = Transitions.Select(c => c.Entering ? enter : exit);

node.TransitionOrder = 10; // 与另一项比较，高值在转场期间显示于上方。
node.TransitionChanged += state => UpdateExteriorDecoration(state);
```

Simple 的 from/to 是明确的起止帧，不自动反转；Offset 是像素位移，RelativeOffset 是当前目标 Size 倍数，Scale/Alpha 乘以资源原值。Fade、SlideHorizontal、ScaleFade 同样支持 delay、transition、ease。位移播放和恢复使用原锚点偏移，保留布局与窗口尺寸变化；缩放恢复原 pivot。配置无效直接报错，所有表现均在完成或失败后恢复。

Node 提供 IsTransitioning、CurrentTransitionPhase、TransitionProgress 和包含 Context/Stage/Progress 的 TransitionState 快照。Stage 区分 Preparing/Playing/Completed/Canceled/Failed；自定义运行对象通过 `context.ReportProgress(value)` 报告有限且单调的 0..1 动画时间。进度包含动画延迟，不包含资源加载；自定义配方不报告时只提供起止信息，未伪造中间值。false 操作为 Preparing→Completed，没有动画播放阶段。

观察回调同步且只读框架，可以调整项目自己的装饰表现；异常继续传播。动画完成不代表提交后通知和清理已成功，最终结果仍以 await 容器 API 为准。此处提供运行能力，没有引入诊断分析系统。遮罩策略和输入策略继续使用原有契约，本次没有扩展。

- 初始化一次；进入/退出可重复；隐藏保留实例，销毁结束子树。
- 栈保留覆盖项，出栈销毁；Tab 保留所有绑定项；独立面板按实例关闭。
- 同容器并发或重入直接抛错，未引入操作队列。
- 所有异步操作可等待；节点销毁取消其操作并等待清理。
- 原始异常记录一次并向上传播，不包装为统一异常，不吞错、不降级。
- 清理同样失败时，原始主异常仍抛出；附加原异常可通过 `UIFailure.GetSecondaryExceptions(ex)` 获取，并单独打印日志。
- 实际后端 API 必须在宿主 UI 线程调用；Godot 会明确校验。初版不自动把后台调用调度到场景线程。

Godot 显示句柄分 owned（动态资源根）与 borrowed（内嵌控件）。借用句柄销毁仅结束逻辑管理，不单独释放父场景资源。

## 资源预加载

资源预加载不创建节点或执行生命周期，普通开窗继续自动取得资源：

```csharp
var prepared = await ui.PreloadAsync<InventoryWindowView>();
var backpack = await windows.OpenAsync<InventoryWindowView>();
prepared.Dispose(); // 窗口独立持有资源，继续正常使用。
await windows.CloseAsync(backpack); // 归还实例持有。
```

同一 Context、同一路径的加载合并；每个预加载句柄/实例独立持有，最后一个持有释放后归还资源。取消一个等待者不影响其他等待者，Context.DisposeAsync 取消并等待所有共享加载收尾、释放剩余句柄。适配器实现 IUIResourceLoader.LoadAsync 返回 IUIResource，其同步 Instantiate 创建 IUIElement，Dispose 归还资源引用；旧加载器签名已移除。

## 当前边界

当前实际验证了 Godot 桌面端，其他引擎需要适配器。Godot 场景使用 ResourceLoader 后台加载，场景实例化和节点初始化仍在主线程；取消底层请求需要等待 Godot 加载结束。释放归还框架引用，实际内存回收受其他引用和引擎/GC 影响。编辑器节点配置、运行时重挂载、缓存预算/池化、绑定框架及诊断分析均未实现。

素材来源与许可见 [Demo 资源记录](Demo/Godot/Assets/SOURCES.md)。
