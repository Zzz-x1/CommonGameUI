# 游戏客户端 Samples

## 星庭远征：Godot

从仓库根执行 `./scripts/Get-Godot.ps1` 获取固定便携引擎，然后 `./scripts/Run-Demo.ps1`。已有引擎可传 `-GodotPath`。源码位于 Demo/Godot，项目为 HierarchyDemo.csproj 和 project.godot。运行脚本需要 PowerShell 与 .NET 10 SDK。

包含本地登录、大厅、六角色、装备培养、背包、编队、关卡出战确认、任务／成就、商店、邮件、好友、活动、资料和设置。纯 UI，不联网、不执行战斗。玩家数据在 user://client-save.json；自动测试使用隔离档案。

观看大厅入场、角色培养和奖励，场景中的 AnimationPlayer 有可编辑轨道；尝试背包↔商店、邮件附件↔商店奖励，观察同页来源分支。完整／简化演出、跳过、分类收起和连续物品图片动画都属于实际游戏流程。

`./scripts/Test.ps1` 执行核心、生成器、Godot headless 与独立进程重启验证。单独引擎自测使用 `./scripts/Run-Demo.ps1 -SelfTest -Headless`。素材来源在 Demo/Godot/Assets/SOURCES.md，不推断其他素材的许可证。

## Unity Demo

先执行 `./scripts/Run-UnityDemo.ps1 -PrepareOnly` 准备核心 DLL，再手动打开 Demo/Unity。场景为 Assets/Scenes/Client.unity。包含大厅、角色、装备、背包、编队、商店、任务与设置，不是 Godot 全模块的移植。

当前时间轴资源有待验证项，最新适配变化仅离线编译；请先阅读 [Unity 适配](../integrations/unity.md)。本次文档不新增原生验证结论，也不自动启动编辑器。

## 独立入门示例

docs/examples/godot 可直接构建运行；docs/examples/unity 提供完整宿主，Prefab 按 [Unity 教程](../getting-started/unity.md) 创建。两者都不接入游戏存档。

[场景组合](game-ui-recipes.md) · [时间轴](../manual/timeline-animation.md)
