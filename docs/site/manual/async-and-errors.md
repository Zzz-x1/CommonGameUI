# 异步、取消与错误

打开、转场、等待答案和关闭用 await；注册、查询和配置保持同步。

## 用法

异步任务直接 await，或交给帧入口观察。正常退出先清理 Context，再销毁引擎挂载。

```csharp
await pages.PushAsync<InventoryView>(cancellationToken: token);
await modals.CloseToAsync(shop, playAnimation: false, cancellationToken: token);
await ui.DisposeAsync();
// When handling an exception at an application boundary, secondary cleanup
// failures remain available as original exceptions:
// IReadOnlyList<Exception> secondary = UIFailure.GetSecondaryExceptions(error);
```

## 打开页面，等待关闭后继续

例如玩家关闭背包后，调用方继续刷新大厅。`OpenAsync／PushAsync` 只等待打开；`WaitForCloseAsync` 等待玩家或其他代码结束这次打开：

```csharp
using UIFramework;

var inventory = await pages.PushAsync<InventoryView>();
await inventory.WaitForCloseAsync(token);
// Continue the caller's flow after inventory closes.
```

同一扩展适用于 Panel、Stack 和 Modal，两端引擎用法一致。等待包含退出动画、关闭通知及缓存归还／销毁清理；Hide、覆盖或失活不会完成它。KeepAlive 关闭也会完成，不能用 Lifetime 替代。

调用时捕获当前打开会话；缓存重开是新会话，不改变旧等待。重复 Single 打开等待同一会话。取消 Token 只取消当前等待，不关闭界面或取消其他等待。关闭准备失败时窗口仍在，等待继续；关闭提交后的清理失败原样传播。直接销毁或父级清理也会在该窗口清理完成后结束等待。

在外部流程中等待，不在自身、祖先或子节点的活动生命周期内等待自身关闭。未通过 Panel／Stack／Modal 打开的 Root、结构容器和内嵌 Tab 没有打开会话，不能使用此接口。需要业务答案时仍使用 `ShowDialogAsync`。

## 一次提交多个关闭请求

离开装备详情时关闭两张对比浮窗。请求立即入队，按顺序关闭；`WhenAll` 等待全部结果，不吞异常。

```csharp
var closeLeft = windows.CloseAsync(left);
var closeRight = windows.CloseAsync(right);
await Task.WhenAll(closeLeft, closeRight);
```

## 要点

- 同容器结构操作按提交顺序排队；不同容器可并行。每个任务等待自己的动画和清理完成。
- 生命周期中请求当前操作所属容器或祖先仍报重入错误；初始化子容器可以等待其操作。
- 排队时取消不会执行该请求；开始执行后取消会等待恢复和清理。销毁父节点会取消并等待整个队列。
- 普通失败只影响该请求；若恢复显示、输入或层级失败，容器停止接收后续操作，必须显式销毁。
- 提交前失败恢复原状态；提交后通知／清理失败保留已提交状态。
- 原异常记录并传播；附加清理异常通过 UIFailure 获取，取消保持取消。
- 不用 Wait／Result 或未观察任务；帧入口仅在 IsCompleted 后取结果。
- 外部重复 Dispose 共享结果；生命周期中不能销毁自己或祖先。

[生命周期](lifecycle.md) · [常见错误](../troubleshooting.md)
