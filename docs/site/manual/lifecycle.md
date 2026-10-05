# 生命周期

初始化时绑定控件，进入时刷新数据，失活时暂停装饰，销毁时释放资源。

## 用法

Entering／Exiting 是可等待的准备；Entered／Exited 是操作提交后的同步通知。

```csharp
public sealed class LobbyView : UINode
{
    protected override Task OnInitializeAsync(CancellationToken token) => Task.CompletedTask;
    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        // Refresh from the project model; context.IsFirstEnter identifies first entry.
        return Task.CompletedTask;
    }
    protected override void OnActivated(StateChangeContext context) { /* Resume decoration. */ }
    protected override void OnDeactivated(StateChangeContext context) { /* Pause decoration. */ }
    protected override Task OnDisposeAsync() => Task.CompletedTask;
}
```

## 要点

- Initialize 一次，进入／退出可重复；隐藏不销毁，Dispose 结束子树。
- IsVisible 表示有效可见，IsActive 表示稳定激活，IsInteractive 还考虑临时输入锁。
- 通知只读框架，不能在其中修改容器或销毁自己／祖先。
- AddLifecycle 在挂载前注册独立行为；空闲时可 RemoveLifecycle，已初始化行为仍在销毁时清理。
- 失败时的状态规则见[异步与错误](async-and-errors.md)。

[API 阶段签名](../api/core.md) · [失败](async-and-errors.md)
