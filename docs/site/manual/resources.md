# 资源管理与预加载

## 使用场景

登录资源准备期间预加载大厅和背包；打开窗口自动取得资源，预加载句柄释放后正在显示的窗口继续使用。

## 资源与职责

IUIResourceLoader 取得可实例化的 IUIResource；Instantiate 在 UI 线程创建 IUIElement。资源持有与显示实例分开，UIResourceLease 不创建 Node 或执行生命周期。

## 使用代码

```csharp
var prepared = await ui.PreloadAsync<InventoryView>(cancellationToken);
var inventory = await windows.OpenAsync<InventoryView>();
prepared.Dispose();
await windows.CloseAsync(inventory);
await ui.DisposeAsync();
```

## 行为与限制

同一 Context、同一路径合并共享加载，各等待者／实例独立持有。一个等待者取消不取消其他人的请求；最后持有释放后归还资源。无路径节点不能预加载。Context 销毁取消并等待共享加载收尾、清理实例和剩余资源。Godot 后台加载，实例化与初始化仍主线程；Godot／Unity 底层请求不可直接取消，取消可能需等底层完成。归还框架引用不承诺立即释放纹理或引擎缓存。没有缓存预算、池化或 Addressables 后端，项目可实现 loader。

## 相关章节

[自定义引擎](../integrations/custom-engine.md) · [异步关闭](async-and-errors.md)
