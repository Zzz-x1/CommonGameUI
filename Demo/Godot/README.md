# WAYFARER · Godot UI Demo

这是虚构冒险游戏的纯 UI 原型，长期随框架迭代。使用 Godot 4.7.2 .NET 和 .NET 10；核心仍为 netstandard2.1。没有角色移动、战斗等 Gameplay。

## 启动

仓库根目录运行 `./scripts/Run-Demo.ps1`，或导入本目录的 `project.godot`，构建 C# 后运行 Main 场景。

## 操作与预期

| 游戏入口 | 操作 | 展示的框架行为 |
| --- | --- | --- |
| Camp overview | 查看营地主菜单，返回主界面 | 项目自行定义菜单与容器，不依赖默认 HUD/Overlay |
| Preferences | 调整画质、音量或灵敏度，按名称切换三个标签 | 内嵌基础 Node + PageActivity；不加载/克隆；控件与会话偏好保留 |
| Backpack | Equipment/Materials 切换，点物品格或 Inspect，收藏后返回 | 栈保留原背包与选中 Tab；收藏筛选生效；详情显示所选物品 |
| Settings | 打开设置，进入画质页，返回，再关闭 | 一个模态外壳/遮罩，内部栈；首次进入与返回提示不同；恢复输入/焦点 |
| Compare relics | 打开两张藏品窗口，分别关闭 | 独立多实例，只关闭目标对象 |
| Return to camp | 可再开一层确认；取消或确认内层，再结束外层 | 嵌套模态结果，只有顶部交互；焦点逐层恢复；外层确认返回营地 |

背包的 Inspection time 只在选中页面有效激活时更新：详情覆盖或模态阻挡时暂停，返回后继续。阶段、原因、实例 ID 输出普通 Godot 日志，不设专门诊断面板。偏好和收藏由项目 `GameUiState` 保存，跨页面重建保留；只是本地 UI 会话数据，未接入真实渲染设置、磁盘存档或 Gameplay。

`Scenes/Preferences/Preferences.tscn` 和背包场景直接包含 Tab 页面，可在 Godot 编辑器开关节点预览，运行时显式选择初始项。页面无需定义空 View 类。`Scripts/Views.cs` 演示业务覆写与处理器组合；原始 Enter/Exit 钩子已迁移为 V2 前置和完成阶段。

初始化使用 `GodotUI.Create(this)`、`Root.CreateContainerAsync<T>`、`AttachToAsync` 和 Control 版 `BindAsync`。基础页挂处理器时先构造 `UINode`、同步 `AddLifecycle`，再绑定已有 Control。便利挂载借用场景控件；动态 owned 挂载由框架释放。上下文必须显式销毁。

## 自动验证

```powershell
./scripts/Test.ps1
./scripts/Run-Demo.ps1 -Headless -SelfTest
./scripts/Run-Demo.ps1 -SelfTest
./scripts/Run-Demo.ps1 -Capture -CaptureScene Inventory
./scripts/Run-Demo.ps1 -Capture -CaptureScene Settings
./scripts/Run-Demo.ps1 -Capture -CaptureScene Preferences
```

自测覆盖内嵌 Tab、背包/详情/收藏/激活暂停与恢复、设置返回与偏好保存、独立实例、嵌套结果/焦点、取消与根清理，成功输出 `GODOT DEMO SELF-TEST PASSED (6 scenarios)`，退出 0。

适配器自测还验证服务替换、默认 Root、Control-only 基础节点、实例/所有权、取消、初始化原始失败和淡出后的转场恢复。预期取消及故意初始化失败仍打印原异常，测试明确断言，未当成技术故障忽略。取消加载保留在自测中，游戏菜单不包含技术测试按钮。

故障夹具分别运行，必须打印原始异常并退出 1：

```powershell
./scripts/Run-Demo.ps1 -Headless -Failure tab
./scripts/Run-Demo.ps1 -Headless -Failure duplicate
./scripts/Run-Demo.ps1 -Headless -Failure load
./scripts/Run-Demo.ps1 -Headless -Failure lifecycle
./scripts/Run-Demo.ps1 -Headless -Failure transition
./scripts/Run-Demo.ps1 -Headless -Failure entered
./scripts/Run-Demo.ps1 -Headless -Failure state
./scripts/Run-Demo.ps1 -Headless -Failure cleanup
```

同步信号成功时不创建空任务；信号错误进入统一帧观察入口。异步任务保存并观察，异常记录、重新抛出且结束失败运行，没有 async void 或 fire-and-forget 隐藏异常。

## 验证记录：2026-10-02 / V2

- Windows 11、.NET SDK 10.0.400、Godot 4.7.2 .NET。
- 解决方案构建：0 警告、0 错误；核心与生成器 41 项测试通过。
- Godot headless 和原生 OpenGL 自测均通过；嵌套模态捕获/恢复焦点由真实引擎断言。
- 8 个独立故障用例均退出 1，保留原异常及堆栈。
- 原生背包、画质弹窗、偏好界面截图已生成并目视检查；截图输出在被忽略的 artifacts。
- 未验证全部分辨率、手柄设备与导出平台；未验证其他引擎。

继续使用用户选择的 Kenney UI Pack - Adventure 和 Nunito 字体。样式在 `Themes/Adventure.tres`，素材来源、作者和原许可在 `Assets/SOURCES.md`。本版复用本地已许可素材，没有运行时在线资源依赖。
