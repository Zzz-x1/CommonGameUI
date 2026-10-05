# 页面历史

## 使用场景

大厅→背包→物品详情→返回，恢复同一个背包实例和筛选状态；“回大厅”直接返回最早的大厅实例。

## 资源与职责

每个页面使用独立资源，挂在同一个 Stack。History 保留被覆盖节点；它们隐藏而未销毁。项目持有大厅实例作为回大厅目标。

## 使用代码

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

## 行为与限制

Push 新建并覆盖当前项；Pop 销毁当前项并恢复前项，必须存在更早项。PopTo 只接受本栈中更早的具体实例，不接受当前项或外部对象；中间项提交后清理，不逐层重新显示。顶层执行退出、目标执行返回准备／进入，中间隐藏项不重播显示生命周期。框架不提供 Replace；跳转政策由游戏流程明确表达。

## 相关章节

[生命周期](lifecycle.md) · [返回规则](input-and-focus.md)
