# 星庭远征 · Unity Demo

Unity 6000.3.24f1 / uGUI 的纯 UI 客户端，使用与 Godot 共用的 Core。

## 运行

安装 .NET 10 SDK，从仓库根执行：

```powershell
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

然后手动打开 Demo/Unity，运行 Assets/Scenes/Client.unity。首次打开前必须准备核心及 MVVM DLL；本地包位于 src/UIFramework.Unity 和 src/UIFramework.Mvvm.Unity。

可选 Unity CLI 操作：

| 参数 | 操作 |
| --- | --- |
| -Play | 启动普通编辑器并进入 Play Mode |
| -SelfTest | 原生 Play Mode 测试 |
| -RebuildAssets | 停止播放后重建场景、Prefab 与 Clip |
| -Resolution | 1280x720／1600x900／1920x1080 |

这些操作会连接编辑器。客户端、适配和已保存时间轴资源通过 26 项原生 Play Mode 回归；状态见[Unity 适配](../../docs/site/integrations/unity.md)。

## 功能

| 入口 | 操作 |
| --- | --- |
| 标题／大厅 | 进入客户端，切换功能；Esc 按顶层返回 |
| 角色 | 选择、更换／卸下装备、消耗材料培养 |
| 背包 | 装备／材料 Tab、详情、最多两张对比卡 |
| 编队 | 四槽选择与清空，禁止重复角色 |
| 商店 | 数量、确认／取消购买，扣款并增加库存 |
| 任务 | 购买后领取一次奖励 |
| 设置 | 内嵌分类、画质弹窗、演出偏好与默认恢复 |

存档：Application.persistentDataPath/unity-client-save.json。保存成功后发布状态，失败直接报错；测试使用隔离档案。

## 动画

- 大厅与奖励：原生 legacy AnimationClip，可在 Animation 窗口编辑。
- 背包↔商店：按真实来源选择横移方向，返回用短动画。
- 商店奖励用时间轴，任务奖励用短缩放；简化模式使用短表现。

Unity 端未包含 Godot 的 Shader 揭开、Partner、粒子或跳过功能。当前 .anim 完整轨道保存尚未重新生成并验证。

## 资源与关闭

Resources/UI 保存业务 Prefab；ClientRoot 用于隔离测试。ClientSceneAuthoring 仅负责编辑器创作，UnityClient 管初始化、返回与任务观察。

销毁 Canvas／EventSystem 前 await ui.DisposeAsync。自动化先等待 ClientAutomation.Shutdown，再停止播放；编辑器强制 Stop 不属于正常关闭。

[接入教程](../../docs/site/getting-started/unity.md) · [素材](Assets/SOURCES.md) · [验收记录](../../docs/design-decisions.md)

生命周期：客户端 Context 通过隐藏管理对象绑定所属 Scene；UnityLifetimeTests 覆盖对象销毁、场景卸载、异步清理和动画取消。使用方法见[生命周期指南](../../docs/site/manual/lifecycle.md)。

隔离且无窗口的原生生命周期测试：`./scripts/Test-UnityLifetimes.ps1`，可通过 `-UnityPath` 指定编辑器路径。

## MVVM 示例

创建 UIContext 即自动启用 MVVM，关闭 Context 时自动清理；Demo 不创建或释放 Runtime。

金币使用共享 ScriptableObject 克隆；详情和设置为节点独立 VM。背包条目使用 ObservableCollection 与 Resources/MVVM/InventoryRow.prefab，购买使用可等待确认的命令，购买窗口的关闭按钮直接绑定 VM 命令并等待关闭自身。Window → UIFramework → View Bindings 打开 GUI；玩家初始模板位于 Resources/MVVM/Player.asset。

[模块入门与 GUI](../../docs/site/manual/mvvm.md)。

## Timeline 过场

大厅首次入场和购买奖励采用真实 Timeline 多轨动画，资产位于 Assets/Timelines。打开对应 .playable 可编辑立绘、面板、文字和装饰轨道；Prefab 的 Transitions/Enter、Exit 配置 PlayableDirector 及本地绑定。其他来源或简化演出沿用条件分支选择轻量动画。制作脚本 ClientTimelineAuthoring.Build 用 Unity 原生 API 保存资源，不在运行时生成轨道。
