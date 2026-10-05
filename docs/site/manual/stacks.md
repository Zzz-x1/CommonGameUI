# 页面历史

大厅→背包→详情→返回，恢复原背包实例；回大厅直接返回保存的大厅实例。

## 用法

每个页面是独立资源，使用同一个 Stack。History 保存被覆盖页面，Current 表示当前页。

```csharp
var lobby = await pages.PushAsync<LobbyView>();
var inventory = await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopAsync();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("shield-01"));
await pages.PopToAsync(lobby);
var current = pages.Current;
var history = pages.History;
```

## 要点

- Push 新建页面，隐藏当前页；Pop 销毁顶层并恢复前页。
- Pop 需要更早的页面；PopTo 只接受本栈中更早的实例。
- PopTo 清理中间页，只恢复目标，不逐层显示。

[生命周期](lifecycle.md) · [返回规则](input-and-focus.md)
