# 资源管理与预加载

进入大厅前准备背包资源；窗口打开后有自己的持有，不依赖预加载句柄继续存在。

## 用法

Preload 只取得资源，不创建 Node。Loader 加载，Resource 实例化，Element 表示显示对象。

```csharp
var prepared = await ui.PreloadAsync<InventoryView>(cancellationToken);
var inventory = await windows.OpenAsync<InventoryView>();
prepared.Dispose();
await windows.CloseAsync(inventory);
await ui.DisposeAsync();
```

## 要点

- 同一 Context 按路径共享加载，各句柄／实例独立持有，最后持有释放后归还资源。
- 一个等待者取消不影响其他等待者；无资源路径不能预加载。
- Context 销毁等待加载收尾并清理资源。底层请求不可取消时，取消可能延迟完成。
- Godot 默认 loader 在场景线程同步调用 `ResourceLoader.Load`；`LoadAsync` 不代表后台加载。加载无法中断，结束后检查取消令牌。
- 释放框架引用不保证引擎立即回收纹理或缓存。

[自定义引擎](../integrations/custom-engine.md) · [异步关闭](async-and-errors.md)

## 关闭后缓存

背包关闭后再打开，可保留分类、滚动和整个子树；装备详情可复用同一实例显示另一件装备。注册 `NodeCacheMode.KeepAlive`，容器操作不变。默认 `None` 仍在关闭后销毁。

```csharp
ui.Register<InventoryView>("inventory-resource", cacheMode: NodeCacheMode.KeepAlive);
ui.Register<ItemDetailsView>("item-resource", cacheMode: NodeCacheMode.KeepAlive);

var inventory = await pages.PushAsync<InventoryView>();
await pages.PopAsync();
var same = await pages.PushAsync<InventoryView>();

var details = await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopAsync();
var reused = await pages.PushAsync<ItemDetailsView>(new ItemArgs("shield-01"));
await pages.PopAsync();
await pages.PopAsync();

await pages.ClearCacheAsync<ItemDetailsView>();
await pages.ClearCacheAsync();
await ui.ClearCacheAsync();
```

`same` 与 `inventory`、`reused` 与 `details` 分别是同一个实例。详情继承 `UINode<ItemArgs>`，在 `OnEnteringAsync` 按本次参数刷新内容；控件事件和绑定在初始化中创建一次。

- Panel Close、Stack Pop／PopTo、Modal Close／CloseTo 共用策略。每个容器、每种注册类型只保留最后关闭的一个实例，不跨容器复用。
- 缓存退出 TryGet、History 和 Current，但仍是原容器拥有的子节点。`IsCached` 对整个子树为 true，窗口不可见、不可交互；隐藏项仍属于已打开窗口。
- 重开更新参数并执行入场，不重复初始化。`LifecycleContext.IsCacheReuse` 为 true，`IsFirstEnter` 为 false。内部 Tab、历史、VM 与绑定均保留。
- 缓存期间绑定、集合和已有命令继续工作。关闭不取消 Lifetime；仅显示时运行的任务或动画由退出／失活回调停止。最终销毁才解除绑定、取消任务并释放 VM 和资源。
- Dialog 每次打开有独立结果会话；关闭未回答的会话取消等待。Modal 遮罩按原规则释放，不随窗口缓存。
- ClearCache 不关闭当前或隐藏窗口。父节点、Context、引擎绑定对象销毁时也释放缓存。重开准备失败或取消时销毁该实例，传播原始错误。

LRU、容量预算和通用对象池留待后续。本版不自动淘汰其他类型的缓存。
