# 自定义引擎适配

实现以下接口，将 Core 接入其他 C# 引擎或项目资源后端。

## 接口职责

- IUIHost：UI 线程检查与焦点恢复。
- IUIElement：显隐、输入、绘制顺序和实例释放。
- IUIResourceLoader／IUIResource：加载资源、创建实例、归还持有。
- IUILog：记录原始异常。

```csharp
public interface IUIElement : IAsyncDisposable
{
    void SetVisible(bool visible);
    void SetInteractive(bool interactive);
    void BringToFront();
    object CaptureOrder();
    void RestoreOrder(object order);
}
public interface IUIResourceLoader
{
    Task<IUIResource> LoadAsync(string resource, CancellationToken cancellationToken);
}
public interface IUIResource : IDisposable
{
    IUIElement Instantiate(IUIElement? mount);
}
public interface IUIHost
{
    void VerifyAccess();
    object? CaptureFocus();
    bool ContainsFocus(IUIElement scope, object target);
    bool TryFocus(IUIElement? scope, object? target);
    void ClearFocus();
}
public interface IUILog
{
    void Error(string message);
    void Exception(Exception exception, string context);
}
```

## 接入

```csharp
var ui = new UIContext(host, loader, logger);
var pages = new StackContainerNode();
await pages.AttachAsync(ui.Root, "Pages", mountElement);
ui.Register<LobbyView>("lobby");
await pages.PushAsync<LobbyView>();
await ui.DisposeAsync();
```

host、loader、logger、mountElement 是项目实现。

## 驱动可选的 MVVM 模块

宿主接入 MVVM 时，在游戏逻辑更新后、UI 布局前提供一个固定提交点。下面的 `BeforeUILayout` 是示例引擎的主线程事件，暂停菜单打开时也应触发。为该引擎循环注册一次驱动。

```csharp
var frames = new MvvmFrameDriver();
MvvmModule.RegisterAdapter(typeof(GameUIHost), assets, findBindings, frames.Attach);
gameLoop.BeforeUILayout += frames.Tick;
```

`Attach` 会对共享 Runtime 去重。每次 `Tick` 刷新排队的属性绑定并观察异步错误。Unity 和 Godot 适配器自动完成此接入。[MVVM API](../api/mvvm.md)

## 必须保证

- 实例化在 UI 线程进行，起始隐藏；借用实例不销毁父资源。
- 输入锁保留业务按钮状态；绘制顺序和焦点能够恢复。
- 取消、创建失败和清理传播原异常，不能遗留原生对象。
- 一个实例关闭不能释放其他实例仍持有的资源。

自定义动画实现 IUITransition／IUITransitionRun；遮罩实现 IUIBackdropFactory／IUIBackdrop，并观察 dismiss 返回的任务。接口见 [Core API](../api/core.md)。

用真实引擎验证加载、取消、输入、焦点、动画恢复和资源释放；核心测试不替代引擎测试。

Error 记录错误文本，Exception 记录原始异常及操作上下文。Unity 适配的异常入口使用 Debug.LogException；日志不替代异常传播。
