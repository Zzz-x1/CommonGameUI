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
    void Focus(IUIElement? element);
    void RestoreFocus(object? focus);
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

## 必须保证

- 实例化在 UI 线程进行，起始隐藏；借用实例不销毁父资源。
- 输入锁保留业务按钮状态；绘制顺序和焦点能够恢复。
- 取消、创建失败和清理传播原异常，不能遗留原生对象。
- 一个实例关闭不能释放其他实例仍持有的资源。

自定义动画实现 IUITransition／IUITransitionRun；遮罩实现 IUIBackdropFactory／IUIBackdrop，并观察 dismiss 返回的任务。接口见 [Core API](../api/core.md)。

用真实引擎验证加载、取消、输入、焦点、动画恢复和资源释放；核心测试不替代引擎测试。

Error 记录错误文本，Exception 记录原始异常及操作上下文。Unity 适配的异常入口使用 Debug.LogException；日志不替代异常传播。
