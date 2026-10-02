# WAYFARER · Godot UI Demo

这是虚构冒险游戏的纯 UI 原型，长期随框架迭代。使用 Godot 4.7.2 .NET 和 .NET 10；核心仍为 netstandard2.1。没有角色移动、战斗等 Gameplay。

## 启动

仓库根目录运行 `./scripts/Run-Demo.ps1`，或导入本目录的 `project.godot`，构建 C# 后运行 Main 场景。

## 操作与预期

| 游戏入口 | 操作 | 展示的框架行为 |
| --- | --- | --- |
| Camp overview | 查看营地主菜单，返回主界面 | 项目自行定义菜单与容器；进入菜单预备背包外壳资源，不提前创建背包 |
| Preferences | 调整画质、音量或灵敏度，按名称切换三个标签 | 内嵌基础 Node；不加载/克隆；控件与会话偏好保留 |
| Backpack | 重复点击入口；Equipment/Materials 切换，点物品格或 Inspect，收藏后返回 | 首次创建后归还预加载持有，窗口继续独立持有；重复打开不创建或重播，内部栈保留原背包与选中 Tab |
| Stow backpack | 在背包或详情/手记页收起，再点击 Backpack | Hide 保留完整实例、控件和内部历史；恢复时不重新初始化，隐藏期间预览暂停 |
| Read field notes | 从物品详情查看手记，选择 Return to backpack | PopTo 返回原背包，跳过详情页恢复，并释放详情和手记；Return to item details 仍为单步 Pop |
| Settings | 打开设置，进入画质页，返回；关闭按钮或点击窗口外部关闭 | 内容缩放淡入、内部页面淡入淡出；容器管理遮罩，恢复输入/焦点 |
| Compare relics | 打开两张藏品窗口，分别关闭 | 注册明确标记 Multiple，独立多实例，只关闭目标对象 |
| Return to camp | 可再开一层确认；按钮或点击窗口外部取消，再结束外层 | 默认共享一张遮罩，位于顶层窗口下；外部点击返回正常 false；只有顶部交互，焦点逐层恢复 |

背包的 Inspection time 只在选中页面有效激活时更新：详情覆盖或模态阻挡时暂停，返回后继续。偏好和收藏由项目 `GameUiState` 保存，跨页面重建保留；只是本地 UI 会话数据，未接入真实渲染设置、磁盘存档或 Gameplay。

`Scenes/Preferences/Preferences.tscn` 和背包场景直接包含 Tab 页面，可在 Godot 编辑器开关节点预览，运行时显式选择初始项。页面无需定义空 View 类。`Scripts/Views.cs` 只保留业务初始化、生命周期与控件事件；不包含生命周期计数或调试日志。

V3 资源层级：`InventoryWindow.tscn` 是单实例窗口外壳，BodyMount 挂载 Stack；`Inventory.tscn` 是栈内背包页，包含内嵌 Tab；`Details.tscn` 和 `Provenance.tscn` 是多级查看页面。同一 Node 不在两个容器中复用。Camp overview 或切换 Preferences 会隐藏背包外壳；Preferences 自身仍按正常关闭重建。隐藏背包只保留本次 UI 会话，不是磁盘存档。Close 与下一次创建的验证位于自动测试，游戏界面不添加技术清理按钮。

初始化使用 `GodotUI.Create(this)`、`Root.CreateContainerAsync<T>`、`AttachToAsync` 和 Control 版 `BindAsync`。基础页直接构造 `UINode` 并绑定已有 Control。便利挂载借用场景控件；动态 owned 挂载由框架释放。上下文必须显式销毁。

转场：项目默认 Fade，背包列表与详情使用物品图标连续展开/缩回，列表和详情文字分阶段淡出/淡入；涉及手记时两侧保留原有淡入淡出。窗口收起仍淡出。配置优先级为节点自身、直接管理容器、上下文默认。设置/确认以 Center 内容包装层缩放，遮罩独立淡入/淡出，不跟随内容缩放。BodyMount 裁剪背包内容。

Partner Screen 示例在独立分支 `demo/partner-screen-transition`。进入 Backpack，点击 Equipment 或 Materials 中任意物品格：对应的 56px 图标连续移动、放大为详情的 176px 图片；点击 Return to inventory，图片缩回刚才点击的格子。下方 Inspect 按钮也有图标，使用该按钮时从按钮里的图标展开，收藏过滤不会导致来源格缺失。详情采用图文并排，标题与返回按钮均留在内容区内。

`Scripts/ItemPartnerTransition.cs` 使用现有 IUITransition/IUITransitionRun 与 Source/Target 获取伙伴，没有新增核心 API。先在 Entering 准备详情数据与纹理，再在播放时等待 Godot 延迟布局，转换两侧矩形到 BodyMount 坐标；临时 TextureRect 使用同一纹理，不拷贝/重挂载业务控件，不接受鼠标输入。两侧原图通过 SelfModulate 的 alpha 隐藏，保留布局；成功、取消和失败都恢复原 tint 并移除临时图像。每帧重新读取矩形，时间进度与缓动值分开。该项目示例限定未旋转、尺寸有效的 UI，不是自动共享元素匹配系统。

V5 使用 `Transitions.Select(context => ...)` 直接按 Phase 和 Source/Target 类型选择背包动画；偏好从 Audio 切到 Graphics 时进入页缩放淡入，其他名称切换淡入淡出。基础 Tab 页仍是 Node，未新增空 View 类。内页首次显示使用 `playAnimation:false`，避免外壳和内部初始页叠加转场；后续玩家操作默认播放动画。自测通过同一 API 验证即时 Tab 切换、即时 PopTo 和两种遮罩的即时开关，不增加技术按钮。

动画补齐：设置采用 Simple 的像素位移、缩放、透明度组合及短延迟；Audio→Graphics 使用 Back Out 缓动。详情的 TransitionOrder=10，手记默认 0，详情淡出时露出下方轻微上移淡入的手记；完成后恢复原 sibling 顺序。背包外壳 Hint 随详情的 TransitionChanged 进度淡化，终态恢复原 tint；它是实际界面装饰，不是进度诊断面板。位移恢复锚点偏移而不是尚未稳定的绝对位置，防止新加载 VBoxContainer 页面产生漂移。

V4 遮罩：`Modals.ConfigureBackdrop(GodotBackdrops.Dim(Track), ModalBackdropMode.Shared)`。弹窗场景不再自带 ColorRect；资源根/全屏布局忽略鼠标，内容面板阻挡鼠标。设置的 DismissRequested 关闭窗口，确认的 DismissRequested 执行 Complete(false)；应用持有并观察点击产生的任务。逐层加深模式使用 PerModal，自测覆盖，日常演示选择共享模式。

## 自动验证

```powershell
./scripts/Test.ps1
./scripts/Run-Demo.ps1 -Headless -SelfTest
./scripts/Run-Demo.ps1 -SelfTest
./scripts/Run-Demo.ps1 -Capture -CaptureScene Inventory
./scripts/Run-Demo.ps1 -Capture -CaptureScene Settings
./scripts/Run-Demo.ps1 -Capture -CaptureScene Preferences
./scripts/Run-Demo.ps1 -Capture -CaptureScene Notes
./scripts/Run-Demo.ps1 -Capture -CaptureScene Dialogs
./scripts/Run-Demo.ps1 -Capture -CaptureScene Partner
```

自测覆盖原有 22 场景及新增资源持有流程，成功输出 `GODOT DEMO SELF-TEST PASSED (23 scenarios)`，退出 0。资源验证检查共享加载不创建实例、预加载释放不影响开窗、关闭最后实例归还资源及 Context 回收剩余预加载。Partner 断言采样两种不同物品纹理、实际矩形插值、源/目标图像不重复、临时图像不截获输入、进入/返回取消、观察回调原始 IOException、失败后历史恢复和即时显示。既有验证继续检查显示对象没有克隆、窗口与内页实例保持、历史目标和 Tab/选中项保持、中间项未再次进入且最终销毁；关闭动画仍可能等待资源，就绪后的即时显示仍完成生命周期、输入交接和清理。Partner 截图模式生成列表、展开中、详情和返回中四张原生截图，输出到忽略的 artifacts。

适配器自测还验证服务替换、默认 Root、Control-only 基础节点、实例/所有权、取消、初始化原始失败和淡出后的转场恢复。预期取消及故意初始化失败仍打印原异常，测试明确断言，未当成技术故障忽略。取消加载保留在自测中，游戏菜单不包含技术测试按钮。

正常 Demo 不捕获和手工处理运行异常：同步事件直接调用业务代码，异步任务在帧入口读取已经完成的结果，原始异常直接交给 Godot 报错。没有自定义错误日志、强制退出、异常包装或失败保底。

业务代码集中于 `Scripts`，实际引擎自测集中于 `Tests/DemoApp.Tests.cs`，显式截图模式集中于 `Tools/DemoApp.Development.cs`。测试中的预期取消/异常捕获只用于断言结果，不参与普通游戏 UI 流程。原 `-Failure` 故障注入入口和 FailureView 已删除；错误契约由核心测试与引擎自测验证。

## 验证记录：2026-10-02 / 资源流程与预加载

- 构建 0 警告、0 错误，核心/生成器 138 项通过；Godot headless 和原生 OpenGL 均通过 23 场景，退出 0。
- 菜单预加载背包外壳，开窗后同步归还预加载凭据；内部页面按需取得，Hide 保留实例资源，没有新增缓存面板、技术按钮或人工业务延时。
- 后台加载使用 ResourceLoader.LoadThreadedRequest 与逐帧状态等待，实例化/挂载保留在场景线程；原生 Partner 四阶段截图已生成，详情截图目视确认布局正常。
- 取消/失败测试继续传播原始异常；延时加载仅存在于测试。Godot 无中途中止接口，Context 释放需等待请求收尾。释放归还框架引用，不承诺立即回收内存/显存；未验证大资源性能、全部分辨率、导出平台、实体手柄或其他后端。

## 历史验证记录：2026-10-02 / Demo 简化

- 业务入口由941 行缩减为 144 行；移出自测和截图流程，删除故障注入分支、生命周期计数和仅用于打印状态的 PageActivity。
- 伙伴动画直接使用既定 View/场景引用，删除类型、纹理匹配、有限值及尺寸的重复检查；创建时就持有确定的临时图像，恢复时直接移除并恢复原表现，不维护手工异常汇总或空对象跳过。
- 构建 0 警告、0 错误；核心 127 项测试通过；Godot headless 与原生 OpenGL 自测通过 22 场景，新增异步错误原样从帧入口抛出的断言。
- 伙伴原生截图已检查，展开、返回效果保留。仍未验证所有分辨率、实体输入设备与导出平台。

## 历史验证记录：2026-10-02 / Partner Screen Demo 分支

- 构建 0 警告、0 错误；核心与生成器 127 项测试通过，未修改核心或引擎适配 API。
- Godot 4.7.2 .NET headless 和原生 OpenGL 自测均通过 21 场景；新增两类 Partner 场景，故意取消与故意回调失败仍记录并断言原始异常。
- 原生列表、展开中、详情、返回中截图已目视检查。修正新增图片造成的 VBoxContainer 最小高度溢出：列表精简冗余标签，详情改为图文并排；分阶段淡化文字避免同时重叠。
- 新增 Lorc/Delapouite 的 CC BY 3.0 图标，修改说明、来源与许可在 Assets/SOURCES.md，菜单显示署名。素材本地化；截图、日志、工具和构建产物不入库。
- 此分支复刻伙伴图像连续转场机制，没有运行 USN 项目或宣称其示例像素级一致。未验证全部分辨率、任意旋转布局、实体输入设备或导出平台。

## 历史验证记录：2026-10-02 / 动画能力补齐

- 构建 0 警告、0 错误；核心与生成器 127 项测试通过，新增运行状态、取消/异常、观察回调失败、进度范围/单调性及绘制优先级/冻结/恢复测试。
- Godot headless 和原生 OpenGL 自测均通过 19 场景。新增像素/相对偏移、镜像缩放、透明度、延迟和零时长、Quart Out 采样、AnimationPlayer 中间进度、延迟取消恢复及节点排序的实际引擎断言。非法配置均明确断言原异常。
- 22 个故障夹具均打印异常并退出 1；新增非法帧参数、非法进度、观察回调原始失败与 AnimationPlayer 被提前停止。
- 手记截图发现的锚点布局漂移已通过偏移播放/恢复重构修复，并加入页面位置/大小与父挂载匹配的回归断言；原生设置、手记与偏好截图已目视检查。复用原素材，日志和截图不入库。
- 未运行 USN 项目、验证所有分辨率、实体手柄、导出平台或其他引擎。没有新增输入/遮罩策略或生命周期处理器顺序；Godot 加载仍同步。

## 历史验证记录：2026-10-02 / V5

- 构建 0 警告、0 错误；核心与生成器 108 项测试通过，生成方法已编译执行验证参数、动画开关、取消和结果透传。
- Godot headless 与原生 OpenGL 自测通过 15 场景；新增选择函数阶段/来源、即时内页、即时返回清理，以及 Shared/PerModal 即时遮罩的实际引擎断言。
- 18 个故障夹具均打印异常并退出 1；新增选择函数抛错、返回 null 和循环选择。原始异常继续向调用方传播。
- 原生偏好、手记截图已目视检查；无新增素材。截图与日志不入库。
- 未验证全部分辨率、实体手柄、导出平台或其他引擎；资源加载仍同步，AnimationPlayer 的业务副作用仍由项目恢复。

## 历史验证记录：2026-10-02 / V4

- 构建 0 警告、0 错误；核心与生成器 86 项测试通过。
- Godot headless 与原生 OpenGL 自测通过 12 场景；适配器同时验证滑动方向、原表现恢复、取消和 AnimationPlayer。
- 共享/逐层遮罩排序、GuiInput 信号点击取消、设置外部关闭、强制释放、焦点恢复均通过实际引擎断言。未把信号回调验证当作实体鼠标/手柄设备测试。
- 15 个故障夹具均打印异常并退出 1；新增动画目标缺失、遮罩创建、外部关闭业务失败。
- 原生设置与嵌套确认截图已目视检查；共享遮罩不逐层加深背景。素材复用原许可，截图与日志不入库。
- 未验证所有分辨率、导出平台、实体手柄和其他引擎；资源加载仍同步。AnimationPlayer 的项目副作用由显式恢复回调负责。

## 历史验证记录：2026-10-02 / V3

- Windows 11、.NET SDK 10.0.400、Godot 4.7.2 .NET。
- 构建 0 警告、0 错误；核心与生成器 59 项测试通过。
- Godot headless 和原生 OpenGL 自测均通过 9 场景。
- 12 个故障夹具均打印异常并退出 1；新增窗口传参、非法显示状态、栈目标和返回清理失败。
- 原生背包、物品手记截图已目视检查；新增外壳后的标题重叠、按钮越界已修正。
- 素材仍复用 Adventure/Nunito，没有新增下载；截图、日志和工具不入库。
- 未验证全部分辨率、实体手柄、导出平台及其他引擎。Godot 加载仍同步，隐藏资源待 Close/Dispose 才释放。

## 历史验证记录：2026-10-02 / V2

- Windows 11、.NET SDK 10.0.400、Godot 4.7.2 .NET。
- 解决方案构建：0 警告、0 错误；核心与生成器 41 项测试通过。
- Godot headless 和原生 OpenGL 自测均通过；嵌套模态捕获/恢复焦点由真实引擎断言。
- 8 个独立故障用例均退出 1，保留原异常及堆栈。
- 原生背包、画质弹窗、偏好界面截图已生成并目视检查；截图输出在被忽略的 artifacts。
- 未验证全部分辨率、手柄设备与导出平台；未验证其他引擎。

继续使用用户选择的 Kenney UI Pack - Adventure 和 Nunito 字体。样式在 `Themes/Adventure.tres`，素材来源、作者和原许可在 `Assets/SOURCES.md`。本版复用本地已许可素材，没有运行时在线资源依赖。
