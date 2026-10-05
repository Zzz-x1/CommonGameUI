# 异步、取消与错误

## 使用场景

按钮打开资源、等待确认、转场和关闭需要等待；注册、查询、配置与完成答案无需异步包装。退出必须在销毁引擎挂载前清理 Context。

## 资源与职责

业务入口 await 操作，或把 Task 交给帧入口集中观察。生命周期准备与动画使用 CancellationToken；Node Lifetime 随销毁取消。

## 使用代码

```csharp
await pages.PushAsync<InventoryView>(cancellationToken: token);
await modals.CloseToAsync(shop, playAnimation: false, cancellationToken: token);
await ui.DisposeAsync();
// When handling an exception at an application boundary, secondary cleanup
// failures remain available as original exceptions:
// IReadOnlyList<Exception> secondary = UIFailure.GetSecondaryExceptions(error);
```

## 行为与限制

同容器并发或重入直接报错，不排队、不重试。预提交失败恢复原状态；提交后通知／清理失败不撤销已经提交的状态。框架记录上下文并传播原异常，不包装、不默认成功。清理失败不覆盖主异常，附加原异常通过 UIFailure 获取；正常 Demo 业务不手工 catch。若应用边界需要捕获后重抛，使用 throw;。取消传播取消，不伪造业务结果。外部重复 Dispose 共享任务和结果，生命周期中自我或祖先销毁仍禁止。禁止 Wait／Result、未观察 fire-and-forget 和 async void 清理；帧观察仅在 IsCompleted 后 GetResult。

## 相关章节

[生命周期](lifecycle.md) · [常见错误](../troubleshooting.md)
