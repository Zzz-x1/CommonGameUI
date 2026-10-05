# 自定义引擎适配

## 何时需要

在另一种 C# 引擎复用同样的窗口／历史逻辑，或替换现有引擎资源后端时，实现最小契约；不用让 Core 引用引擎类型。

## 必需接口

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
    void Error(Exception exception, string context);
}
```

Host 校验 UI 线程，捕获可恢复的焦点句柄；目标销毁后不能访问失效原生对象。Element 管显隐、输入和绘制顺序；输入锁保留业务按钮条件，RestoreOrder 恢复 CaptureOrder 对应状态。DisposeAsync 区分 owned 动态实例与 borrowed 内嵌实例，借用不销毁父资源。

Loader 等待真实资源请求，Resource.Instantiate 同步在 UI 线程创建隐藏的显示实例并挂到 mount。请求取消、失败和释放都保留原异常；创建失败时清理已创建原生对象。资源归还与实例销毁独立，不能让一个实例关闭释放其他实例仍使用的资源。

## 创建 Context

```csharp
var ui = new UIContext(host, loader, logger);
var pages = new StackContainerNode();
await pages.AttachAsync(ui.Root, "Pages", mountElement);
ui.Register<LobbyView>("lobby");
await pages.PushAsync<LobbyView>();
await ui.DisposeAsync();
```

host、loader、logger、mountElement 是项目实现。核心不定义固定资源格式或业务挂载。适配日志应保留原异常，Context 负责自己的日志去重与失败流程。

## 动画与模态扩展

实现 IUITransition.Create，每次生成独立 IUITransitionRun；PlayAsync 等待、支持取消，Restore 同步恢复原表现。实现遮罩时通过 IUIBackdropFactory.CreateAsync 接收 dismiss 回调，由宿主观察每一个 Task；IUIBackdrop 的 Show／Hide 支持动画开关，Restore 恢复稳定显隐。不能丢弃关闭请求失败。

## 验证清单

用隔离场景验证：线程检查、借用／拥有对象释放、加载共享、取消晚到资源、输入与焦点恢复、兄弟顺序恢复、遮罩关闭、动画首次等待前的起点，以及主错误和清理错误同时出现。使用真实引擎验证补充核心测试；没有验证的引擎不列为已支持。

[资源](../manual/resources.md) · [API](../api/core.md)
