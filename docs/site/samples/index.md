# 游戏客户端 Samples

「星庭远征」是纯 UI 客户端：包含本地业务与存档，不联网或执行战斗。

## 运行

从仓库根目录执行：

```powershell
# Godot: .NET 10 SDK + Godot 4.7.2 .NET
./scripts/Get-Godot.ps1
./scripts/Run-Demo.ps1

# Unity: prepare DLL, then open Demo/Unity manually.
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

Godot 场景为 Scenes/Client/Main.tscn；Unity 为 Assets/Scenes/Client.unity。Unity 验证状态见[适配说明](../integrations/unity.md)。

## 推荐体验

| 路径 | 展示能力 |
| --- | --- |
| 标题→大厅 | 资源准备、生命周期、时间轴入场 |
| 背包→详情→返回 | 页面历史与实例保持 |
| 设置→画质→返回 | 嵌套弹窗与输入恢复 |
| 商店→购买确认→奖励 | 等待答案、业务提交与演出 |
| 背包装备对比 | 多实例浮窗 |
| 背包↔商店、邮件领取 | 同页按来源选择动画（Godot） |
| 活动收起／展开 | 保留内嵌页面（Godot） |

Godot 包含角色、背包、编队、关卡、商店、任务、邮件、好友、活动、资料和设置。Unity 覆盖角色、背包、编队、商店、任务和设置。

## 示例与测试

- 小型教程：docs/examples/godot；Unity 宿主：docs/examples/unity。
- 完整检查：`./scripts/Test.ps1`，自动测试使用隔离档案。
- 操作说明与素材记录位于各 Demo 的 README.md 和 Assets/SOURCES.md。

[场景组合](game-ui-recipes.md) · [时间轴](../manual/timeline-animation.md)

两端客户端使用 MVVM 展示共享金币、独立装备详情、设置双向编辑、购买命令和背包条目，见 [MVVM 入门](../manual/mvvm.md)。
