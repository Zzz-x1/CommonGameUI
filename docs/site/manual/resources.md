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
