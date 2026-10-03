# 星庭远征 · Unity Demo

Unity 6000.3.24f1、uGUI，纯 UI 本地客户端，不含 Gameplay。与 Godot Demo 共用渲染无关核心；当前 Unity 版本展示角色、装备、背包、编队、商店、任务和设置，不宣称完整移植 Godot 的全部模块。

## 运行

需要 .NET 10 SDK、Unity 6000.3.24f1 和 Unity CLI。项目使用本地 UPM 包 `src/UIFramework.Unity`。

```powershell
# 从仓库根目录执行：构建核心 DLL，连接普通编辑器并进入 Play Mode。
./scripts/Run-UnityDemo.ps1 -Play

# 原生 Play Mode 验证，不创建 BatchMode 进程。
./scripts/Run-UnityDemo.ps1 -SelfTest

# 只准备 DLL，随后可自行用 Unity 打开 Demo/Unity。
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

场景为 `Assets/Scenes/Client.unity`。脚本复用已连接编辑器，重编译 `focus=false`，不会反复聚焦 Unity。未运行编辑器时第一次启动仍需 `unity open`。`-Resolution 1280x720 / 1600x900 / 1920x1080` 设置 Game View；`-RebuildAssets` 从编辑器创作脚本重建保存的场景、Prefab 和 AnimationClip，必须先停止 Play Mode。

核心 DLL 是构建产物，不入 Git。首次直接打开工程前须执行 `-PrepareOnly`，不能以缺失程序集的编译错误为正常状态。Unity 使用 `netstandard2.1` 二进制核心；这一适配尚未接入 Unity Source Generator 工作流，Demo 注册是普通 C#。

## 操作

- 标题“继续启程”进入大厅，底部入口切换功能；Esc 优先关闭顶层弹窗、对比窗口，再返回页面历史。Unity EventSystem 提供键盘焦点与 Enter 提交。
- 商店选装备、调整数量、确认或取消购买。扣款与增加库存同步保存；奖励关闭后返回原购买窗口。
- 背包切换资源内嵌的装备／材料 Tab，点击装备详情，最多同时打开两张对比卡；关闭后可继续添加。
- 角色选择同行者，选择装备并确认、更换／卸下或消耗金币与手记培养。编队四个位置选择角色或空位，“应用”保存，重复角色不可应用。
- 完成一次购买后可领取“整装待发”，奖励不能重复领取。
- 设置→画面→画质弹窗，关闭恢复原设置实例。画质是本地偏好模拟；演出页切换完整／简化表现，恢复默认需确认。
- 回大厅使用原历史实例；大厅 Esc 确认返回标题。

初始金币 12,000、经验手记 24，每件装备两份，六名角色 Lv.12。存档为 `Application.persistentDataPath/unity-client-save.json`，成功替换文件后才发布状态。损坏、缺字段、写入失败直接报原始异常，不重置档案。自动测试创建独立 GUID 档案，截图使用独立 capture 档案。

## 动画与来源分支

大厅入场由 `LobbyEnter.anim` 编排立绘横移、欢迎面板和入口错峰出现；购买后的奖励由 `RewardEnter.anim` 编排面板、旋转光带和奖励卡，均可在 Unity Animation 窗口编辑。原生 `Animation` 使用非循环 legacy AnimationClip，适配器按未缩放时间采样，Gameplay 的 `timeScale=0` 不阻止 UI 演出。完成、取消和失败恢复原属性以及动画状态；轨道不调用购买或存档。

在完整演出模式体验以下分支：

1. 大厅→背包：缩放入场；背包→商店：右侧横移；商店→背包：左侧横移；Esc：短返回。同一页根据真实 Source／Target／Direction 选择配方。
2. 商店→装备→确认购买：完整奖励时间轴；任务→领取奖励：短缩放。两条流程使用同一个 RewardView，选择器直接读取上下文来源。
3. 设置→演出关闭完整模式：页面和奖励改为短淡入／缩放，不改变扣款、发奖和存档。

分支是项目内 `ClientTransitions.Page` 和 RewardView 中的单个 `Transitions.Select` 函数；没有新增框架来源枚举或动画状态机。Unity 版本目前没有 Godot 的 Shader 揭开、Partner 图片连续转场、粒子或跳过按钮，不能把两端的表现覆盖混为一谈。

## 场景与生命周期

`Resources/UI` 中为保存的业务 Prefab。`ClientRoot.prefab` 与正常场景使用同一外壳，供隔离测试加载。Canvas 下的 PageMount／WindowMount／ModalMount 为项目结构挂载；空挂载不含可射线命中的 Graphic，不阻挡标题。

业务节点为普通 C# UINode；UnityClient 负责宿主、任务观察、项目外壳入口锁与返回。Context 必须在 Canvas／EventSystem 销毁前异步释放。自动化先等待 `ClientAutomation.Shutdown`，再 `editor_stop`。编辑器强制停止 Play Mode 或销毁宿主不是可等待的正常关闭流程。

`Assets/Editor/ClientSceneAuthoring.cs` 仅在编辑器创建资源；正常运行加载保存的 Prefab，不用运行时代码重新搭建整套控件。`Tools/ClientAutomation.cs` 是通过 CLI `run_script` 调用的异步截图／关闭辅助，不属于玩家菜单或业务诊断。

素材说明见 [Assets/SOURCES.md](Assets/SOURCES.md)。框架契约和验收集中于 [唯一设计文档](../../docs/design-decisions.md)。
