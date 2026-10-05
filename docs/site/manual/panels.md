# 窗口与实例管理

背包隐藏后保留筛选和滚动位置；装备对比可同时打开多个实例。

## 用法

Panel 默认单实例。需要多个窗口时，注册为 WindowInstanceMode.Multiple，并保存各实例引用。

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

## 要点

- Open：单实例已有时复用，隐藏时恢复。
- Hide／Show：保留实例并切换显隐。Close：销毁，下次 Open 新建。
- TryGet 包含隐藏项；多个匹配实例会报歧义。
- 重复 Hide／Show 或传入其他容器的节点报错。窗口数量上限由项目控制。

[资源](resources.md) · [游戏场景](../samples/game-ui-recipes.md)
