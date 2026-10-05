# 生命周期

## 使用场景

角色详情首次初始化绑定控件，每次返回刷新模型；大厅被弹窗遮住后暂停持续装饰，恢复后继续。

## 资源与职责

逻辑节点与引擎资源分离。初始化发生在挂载，Entering／Exiting 是提交前可等待的准备，Entered／Exited 是提交后同步通知。Activated／Deactivated 表达有效激活变化，VisibilityChanged 表达有效可见性变化。

## 使用代码

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

## 行为与限制

Initialize 一次，进入／退出可多次；Hide 与被覆盖不销毁，Dispose 结束子树。IsVisible 包含祖先可见性，IsActive 包含稳定输入所有权，IsInteractive 还包含临时锁；动画锁不等于业务停用。准备失败恢复未提交状态；提交后通知或清理失败保留已提交状态并传播原异常。通知和选择器只读框架，不能在其中调用容器修改 API。生命周期中不能销毁自己或祖先。独立行为在挂载前 AddLifecycle(new MyBehavior())，继承 NodeLifecycle 只覆写需要阶段；添加仅限挂载前；移除可在空闲 Ready 状态停止后续通知，已初始化行为仍在节点销毁时清理。

## 相关章节

[API 阶段签名](../api/core.md) · [失败](async-and-errors.md)
