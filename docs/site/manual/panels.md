# 窗口与实例管理

## 使用场景

背包关闭显示后仍要保留筛选与滚动位置；装备对比需要两张独立浮窗。使用 Panel 的隐藏与实例策略，不反复创建背包。

## 资源与职责

独立背包资源与对比卡资源分别注册；WindowInstanceMode 只决定 Panel 的实例行为，不影响 Stack 或 Modal。项目保存对比卡实例引用，并负责两张上限。

## 使用代码

```csharp
ui.Register<InventoryView>("inventory-resource");
ui.Register<CompareView>("compare-resource", windowMode: WindowInstanceMode.Multiple);
var inventory = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(inventory);
var same = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(same);
await windows.ShowAsync(same);
bool exists = windows.TryGet<InventoryView>(out var existing);
var left = await windows.OpenAsync<CompareView>();
var right = await windows.OpenAsync<CompareView>();
await windows.CloseAsync(left);
await windows.CloseAsync(right);
await windows.CloseAsync(inventory);
```

## 行为与限制

默认 Single，重复 Open 返回已有实例，隐藏项会恢复；显示项不重新进入。Hide 保留，Show 恢复，Close 销毁，下次 Open 新建。TryGet 包括隐藏的已提交实例，匹配多个实例报歧义，不任选一张。Hide 已隐藏项、Show 已显示项或操作其他容器的节点直接报错。上限、拖动和浮窗位置都是项目规则。

## 相关章节

[资源](resources.md) · [游戏场景](../samples/game-ui-recipes.md)
