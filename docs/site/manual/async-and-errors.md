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
