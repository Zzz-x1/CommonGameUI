# 星庭远征 · Godot Demo

Godot 4.7.2 .NET / .NET 10 的纯 UI 客户端，包含本地业务与存档。

## 运行

从仓库根目录执行：

```powershell
./scripts/Run-Demo.ps1
```

运行脚本会准备可选 MVVM 插件源码。也可先执行 `./scripts/Prepare-GodotMvvm.ps1`，再导入本目录 project.godot，构建 C#，运行 Scenes/Client/Main.tscn。布局基准 1600×900。

鼠标操作，Tab 移动焦点，Enter 确认；Esc 依次返回弹窗、对比窗、页面、大厅。左上游戏名打开素材署名。

## 功能

| 入口 | 可以做什么 |
| --- | --- |
| 标题 | 打开本地档案；首次启动使用预设玩家，资源准备完成后进入大厅 |
| 大厅 | 切换六名看板角色，查看资源、可领取任务与邮件，进入所有模块 |
| 角色 | 查看六名同行者，进入档案；选择／卸下装备，消耗金币与经验手记培养，观看成长演出 |
| 背包 | 装备／材料分类，搜索、排序、收藏；点击物品连续展开图片；装备可打开对比窗并选择第二件装备 |
| 编队 | 四个槽位，选择、替换、清空角色；已经入队的角色不可重复选择 |
| 冒险 | 六个剧情关卡与四个挑战；查看目的地、队伍、出战确认；结束于 UI 确认，不进入战斗、扣体力或发战斗奖励 |
| 商店 | 装备、材料、礼包；选择数量、确认订单、购买、奖励演出；返回原商品详情，物品进入背包 |
| 任务／成就 | 各十二项；登录、购买、培养、装备、编队等行为推进目标；完成后领取一次性奖励 |
| 邮件 | 八封来信；阅读、单封／批量领取附件、确认删除已领取邮件 |
| 好友 | 六名模拟联系人，搜索、收藏、资料与移除确认；没有在线状态或聊天 |
| 活动 | 庆典、签到、采购三个固定活动；目标可以收起／展开，领取状态保存；签到每个档案一次，不做日历或服务器重置 |
| 资料 | 修改昵称，选择头像与看板角色，查看本地行为统计 |
| 设置 | 声音／画面／演出内嵌页面；音量作用于音频总线，窗口模式实际切换，画质只是 UI 偏好模拟；恢复默认需确认 |
| 返回／退出 | 左上返回或 Esc；大厅确认后回标题，窗口关闭请求需确认退出 |

存档：user://client-save.json。写入成功后刷新 UI；损坏或写入失败直接报错。初始资源足够体验购买、装备、培养与领取奖励。

## 推荐动画路径

| 路径 | 演出 |
| --- | --- |
| 标题→大厅 | 幕帘、背景推镜、立绘与入口错峰入场 |
| 角色→详情→培养 | 星阵、闪光、光带与成长结果 |
| 商店→购买→奖励 | 星阵与奖励卡时间轴 |
| 背包↔商店 | 根据来源选择方向；返回使用短动画 |
| 邮件→单封领取 | 同一奖励窗口使用短揭开 |
| 背包→物品详情 | Partner 图片连续展开 |

设置→演出开启完整模式；长演出可跳过。Lobby、Training、Reward 等场景中的 AnimationPlayer 轨道可在编辑器预览。动画只控制表现，跳过不重复提交业务。

## 代码与资源

- Scenes/Client：界面与内嵌页面；Themes/Client.tres：样式。
- DemoApp：初始化、返回、输入范围与任务观察。
- ClientData：独立模型与存档；ClientViews／ClientModals：业务界面。
- ClientTransitions／ItemPartnerTransition：来源分支与连续转场。
- Tests／Tools：隔离自测与截图。

转场和资源接入见[用户文档](../../docs/site/index.md)，素材见[来源记录](Assets/SOURCES.md)。

## 验证

```powershell
./scripts/Run-Demo.ps1 -SelfTest -Headless
./scripts/Run-Demo.ps1 -VerifySave -Headless
./scripts/Run-Demo.ps1 -Capture -Resolution 1280x720
```

SelfTest 使用隔离档案，VerifySave 在独立进程检查重启状态。Capture 输出各模块和过场帧，可选 1600x900／1920x1080；截图保存在忽略的 artifacts。完整检查使用 scripts/Test.ps1。

[实现与验收记录](../../docs/design-decisions.md)

生命周期：客户端 Context 绑定主场景节点；绑定与清理回归位于 Tests/DemoApp.LifetimeTests.cs。使用方法见[生命周期指南](../../docs/site/manual/lifecycle.md)。

仅运行生命周期回归：`./scripts/Run-Demo.ps1 -LifetimeTest -Headless`（独立测试档案）。

## MVVM 示例

创建 UIContext 即自动启用 MVVM，关闭 Context 时自动清理；Demo 不创建或释放 Runtime。

大厅金币使用 Shared Asset 克隆；设置和装备详情使用节点独立实例；背包使用 ObservableCollection 与条目场景，筛选／排序由项目 VM 维护。购买确认由单个命令提交业务、等待关闭自身确认窗口，再展示奖励；VM 在命令完成后释放。编辑器底部 View Bindings 可编辑条目绑定，原始玩家模板位于 Assets/Mvvm。

[模块入门与 GUI](../../docs/site/manual/mvvm.md)。
