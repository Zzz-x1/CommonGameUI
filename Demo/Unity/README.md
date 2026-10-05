# 星庭远征 · Unity Demo

Unity 6000.3.24f1 / uGUI 的纯 UI 客户端，使用与 Godot 共用的 Core。

## 运行

安装 .NET 10 SDK，从仓库根执行：

```powershell
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

然后手动打开 Demo/Unity，运行 Assets/Scenes/Client.unity。首次打开前必须准备核心 DLL；本地包位于 src/UIFramework.Unity。

可选 Unity CLI 操作：

| 参数 | 操作 |
| --- | --- |
| -Play | 启动普通编辑器并进入 Play Mode |
| -SelfTest | 原生 Play Mode 测试 |
| -RebuildAssets | 停止播放后重建场景、Prefab 与 Clip |
| -Resolution | 1280x720／1600x900／1920x1080 |

这些操作会连接编辑器。当前时间轴资源仍待验证，最新适配修复仅离线编译；状态见[Unity 适配](../../docs/site/integrations/unity.md)。

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
