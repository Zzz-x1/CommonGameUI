# 星庭远征 · Unity Demo

Unity 6000.3.24f1 / uGUI 的纯 UI 客户端，使用与 Godot 共用的 Core。

## 业务脚本

每个窗口脚本单独保存在同名文件，并直接挂载在窗口资源根节点上。继承 `UIFramework.Unity.UINode`，参数窗口使用 `UINode<TArgs>`，确认窗口使用 `DialogNode<TResult>`。注册从资源取得脚本实例；确认文本在注册配置回调中赋值，脚本构造函数不注入服务。

## 运行

安装 .NET 10 SDK，从仓库根执行：

```powershell
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

然后手动打开 Demo/Unity，运行 Assets/Scenes/Client.unity。首次打开前必须准备核心及 MVVM DLL；本地包位于 src/UIFramework.Unity 和 src/UIFramework.Mvvm.Unity。

修改 Core／MVVM 源码后，再执行 `-PrepareOnly` 同步 Unity 包内的 DLL。仅编译核心测试不会更新 Unity 实际引用的程序集。

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
- 模态窗口与新遮罩同时入场；最后一层关闭时同时退出，两者结束后恢复输入。Shared 遮罩在嵌套窗口开关时保留。
- 遮罩悬停或按下不改变颜色；淡入／淡出只改变 CanvasGroup.alpha。
- 背包与商店入场前自动等待初始异步条目与布局完成；显示后的数据变化继续增量更新。

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

角色培养：进入角色详情并点击培养，先保存等级和属性，再暂停 Hero.Power 绑定播放战力／攻击数值增长，动画完成后显式 ResumeBinding 同步最新值。简化演出使用较短计数动画。

商店条目由异步资源工厂创建，借用共享金币与商店来源，点击时读取商品 ID 作为命令参数。金币不足时卡片变暗，仍可进入详情查看。列表移动／删除保持或释放对应实例。

## 关闭后缓存

背包与装备详情启用 KeepAlive：返回后重开背包保留 Tab，点击另一件装备复用详情并刷新 VM。确认弹窗复用原生实例，每次打开有独立文本参数和结果会话。返回标题后的会话清理由项目负责，Context 销毁释放全部缓存。

默认遮罩在 UIContext 上统一配置为 0.2 秒淡入／淡出，Modal 容器自动继承；特殊容器可覆盖、禁用或恢复默认。

离开装备详情时，两张对比浮窗的关闭请求一次提交，由 Panel 按顺序执行；等待全部完成后继续页面返回。

容器挂载：客户端保留场景中显式配置的内容区和绘制层序。简单全区域容器可用 `await ui.Root.CreateContainerAsync<PanelContainerNode>("Windows")` 自动创建挂载；完整示例见 `docs/examples` 两端入门工程。

Unity MVVM：窗口不再维护 ViewModels 列表。全局 SO VM 放入 GlobalViewModelCatalog，目录资产保存到 Resources/UIFramework/Mvvm；绑定行引用资产。客户端商品条目仍使用自动注入的 Item 和父来源映射，不需要 Creation。全局目录、Context 隔离和编辑器预览示例分别见 MvvmNativeTests、MvvmEditorVerification 与用户指南。
