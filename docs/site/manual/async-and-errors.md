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

## 要点

- 同容器并发或重入报错，不自动排队。
- 提交前失败恢复原状态；提交后通知／清理失败保留已提交状态。
- 原异常记录并传播；附加清理异常通过 UIFailure 获取，取消保持取消。
- 不用 Wait／Result 或未观察任务；帧入口仅在 IsCompleted 后取结果。
- 外部重复 Dispose 共享结果；生命周期中不能销毁自己或祖先。

[生命周期](lifecycle.md) · [常见错误](../troubleshooting.md)
