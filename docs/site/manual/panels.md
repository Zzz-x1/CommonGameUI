# PanelContainerNode：独立窗口与实例

用于独立打开、隐藏或关闭窗口：背包隐藏后保留筛选和滚动位置，或同时显示两张装备对比卡。打开一个窗口不会自动隐藏其他窗口，也不会建立返回历史。

## 资源结构

每个窗口使用独立 Prefab／PackedScene。Panel 挂载到项目提供的窗口区域，管理其下的实例：

```text
WindowMount              ← PanelContainerNode 挂载
├─ InventoryView         ← 单实例，显示或隐藏
├─ CompareView(item A)   ← 多实例
└─ CompareView(item B)   ← 多实例
```

## 用法

按[创建与挂载](concepts.md#创建与挂载)创建 `PanelContainerNode`，下例命名为 windows。单实例是默认注册方式；已有实例再次打开时复用。

```csharp
ui.Register<InventoryView>("inventory-resource");
var inventory = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(inventory);
var same = await windows.OpenAsync<InventoryView>();
bool exists = windows.TryGet<InventoryView>(out var existing);
await windows.CloseAsync(inventory);
```

same、existing 和 inventory 是同一个实例。Hide 保留节点与控件状态，Open 或 Show 恢复显示；默认 Close 销毁实例，下次 Open 重新创建；配置 KeepAlive 则[关闭后缓存](resources.md#关闭后缓存)。资源字符串替换为 Godot 的 res:// 路径或 Unity 的 Resources 路径。

## 同时打开多个实例

```csharp
ui.Register<CompareView>("compare-resource", windowMode: WindowInstanceMode.Multiple);
var left = await windows.OpenAsync<CompareView>(new ItemArgs("sword-01"));
var right = await windows.OpenAsync<CompareView>(new ItemArgs("shield-01"));
await windows.CloseAsync(left);
await windows.CloseAsync(right);
```

CompareView 继承 `UINode<ItemArgs>`，每张卡接收自己的装备 ID。

## 行为与边界

- Open：单实例已有时复用，隐藏时恢复。
- Hide／Show：保留实例并切换显隐。Close：默认销毁；KeepAlive 退出窗口列表并缓存。
- TryGet 包含隐藏项；多个匹配实例会报歧义。
- 已隐藏再 Hide、已显示再 Show 正常完成，不重播生命周期或动画；其他容器的节点仍报错。窗口数量上限由项目控制。
- 同一次打开的重复 Close 共享一次执行；正常关闭后再次 Close 成功。首个请求决定动画和执行取消，后续取消只停止自己的等待。关闭失败原样传播，不视为已关闭。
- 多级菜单的自动隐藏和逐级返回使用 [Stack](stacks.md)；阻挡背景输入的确认窗口使用 [Modal](modals-and-dialogs.md)。Panel 不自动恢复“上一个窗口”的输入或焦点。

[容器选择](concepts.md#按流程选择容器) · [资源](resources.md) · [游戏场景](../samples/game-ui-recipes.md)
