# Core API Reference

命名空间：UIFramework。以下列出签名与默认值；用法见 [Manual](../manual/concepts.md)。

## UIContext

Root 为结构根；Register 同步且同类型重复注册报错。resource 为 null 表示无资源节点；属性资源由生成器传入，手写注册必须明确提供路径。DefaultTransition 初始 Instant，不能设 null。Dispose 重复调用共享结果。

```csharp
UIContext(IUIHost host, IUIResourceLoader loader, IUILog? log = null)
UINode Root { get; }
IUIHost Host { get; }
IUIResourceLoader Loader { get; }
IUILog Log { get; }
IUINode? InputOwner { get; } // Set by the framework.
IUITransition DefaultTransition { get; set; }
IUIBackdropFactory? DefaultBackdropFactory { get; set; } // default null.
void Register<T>(string resource, Action<T>? configure = null, WindowInstanceMode windowMode = WindowInstanceMode.Single, NodeCacheMode cacheMode = NodeCacheMode.None) where T : class, IUINode
void Register<T>(Func<T> factory, string? resource = null, WindowInstanceMode windowMode = WindowInstanceMode.Single, Action<T>? configure = null, NodeCacheMode cacheMode = NodeCacheMode.None) where T : class, IUINode
Task ClearCacheAsync(CancellationToken cancellationToken = default)
Task<UIResourceLease> PreloadAsync<T>(CancellationToken cancellationToken = default) where T : class, IUINode
ValueTask DisposeAsync()
```

## Context 模块扩展

可选模块注册一次工厂。Context 自动初始化模块，并在 Root 与资源清理后按反序释放；普通 UI 业务无需调用这些接口。

```csharp
interface IUIContextExtension
{
    void Initialize(UIContext context);
    ValueTask DisposeAsync();
}
void UIContextExtensions.Register(Func<IUIContextExtension> factory);
```

## 共有节点身份

Core 结构节点与引擎业务脚本均实现 `IUINode`。容器的返回值、Children、History、Current、输入所有者和生命周期描述使用实际业务身份。接口的 Name／IsVisible 对应原生基类 UIName／IsUIVisible；业务通常无需访问背后的 Core Node。

```csharp
// IUINode: common state and lifecycle operations listed below.
UINode Node { get; }
IUINode? Parent { get; }
IReadOnlyList<IUINode> Children { get; }
// IParameterizedNode<out TArgs> : IUINode
TArgs Parameters { get; }
// IDialogNode<TResult> : IUINode
void Complete(TResult result);
Task<TResult> WaitForResultAsync(CancellationToken cancellationToken);
// IUIResourceNodeProvider: implemented by native display adapters.
IUINode GetNode(Type businessType);
```

## UINode / `UINode<TArgs>`

普通节点初始隐藏；Context 在未挂载时不可取。状态属性由框架维护；Transition=null 表示继承。AddLifecycle 仅挂载前，RemoveLifecycle 可在静止的 Ready 状态取消后续通知，但已初始化行为仍在销毁时清理。Attach 不支持重挂载。

```csharp
Guid Id { get; }
string Name { get; }
UINode? Parent { get; }
IReadOnlyList<IUINode> Children { get; }
UIContext Context { get; }
IUIElement? Element { get; }
NodeState State { get; }
bool HasEntered { get; }
bool IsCached { get; }
bool IsVisible { get; }
bool IsActive { get; }
bool IsInteractive { get; }
CancellationToken Lifetime { get; }
IUITransition? Transition { get; set; }
int TransitionOrder { get; set; }
TransitionState? TransitionState { get; }
bool IsTransitioning { get; }
TransitionPhase? CurrentTransitionPhase { get; }
float TransitionProgress { get; }
event Action<TransitionState>? TransitionChanged;
Func<CancellationToken, Task>? DismissRequested { get; set; }
void AddLifecycle(INodeLifecycle handler)
void RemoveLifecycle(INodeLifecycle handler)
Task AttachAsync(IUINode parent, string name, IUIElement? element = null, CancellationToken cancellationToken = default, Action? configure = null, bool? initiallyVisible = null)
IUINode GetChild(string name)
ValueTask DisposeAsync()
// UINode<TArgs>:
TArgs Parameters { get; }
```

## UINode hooks

按生命周期顺序覆写；提交后通知只读框架。OnApplyState 与子节点移除钩子用于扩展节点表现／容器清理，正常业务优先标准生命周期。

```csharp
protected virtual bool InitiallyVisible { get; } // default false; Container default true.
protected virtual Task OnInitializeAsync(CancellationToken token)
protected virtual Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
protected virtual void OnEntered(LifecycleContext context)
protected virtual Task OnExitingAsync(LifecycleContext context, CancellationToken token)
protected virtual void OnExited(LifecycleContext context)
protected virtual void OnActivated(StateChangeContext context)
protected virtual void OnDeactivated(StateChangeContext context)
protected virtual void OnVisibilityChanged(StateChangeContext context)
protected virtual Task OnDisposeAsync()

```

<details>
<summary>节点与自定义容器扩展钩子</summary>

```csharp
protected virtual void OnChildRemoved(UINode child)
protected virtual Task OnChildRemovedAsync(UINode child)
protected virtual void OnApplyState()
protected Task RunAsync(string operation, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default, bool allowInitializing = false)
// ContainerNode extension helpers:
protected virtual void OnOperationLockChanged(bool locked)
protected Task<T> OperateAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token, Action? beforeLock = null)
protected LifecycleContext Describe(UINode node, LifecycleReason reason, UINode? source, UINode? target)
protected void CompleteChange(LifecycleContext operation, UINode? exiting, LifecycleContext? exit, UINode? entering, LifecycleContext? enter, Action? focus = null)
protected Task FinishCloseAsync(IUINode node, Action notify)
protected Task PrepareChangeAsync(LifecycleContext? exit, LifecycleContext? enter, CancellationToken token, bool playAnimation, TransitionDirection direction = TransitionDirection.None)
```

</details>


## 容器与结果弹窗

Panel Single 保留隐藏项，Stack 保留历史；Close／Pop 默认销毁，KeepAlive 缓存。Tab 绑定保留控件；Modal 仅顶层输入。PopTo／CloseTo 接收同容器存活实例，当前目标正常完成。ShowDialog 等待结果和关闭清理；外部未回答关闭会取消。RequestDismiss 返回 false 表示未配置处理。

### `ContainerNode : UINode`

子节点的默认转场。

```csharp
IUITransition? ChildTransition { get; set; }
Task ClearCacheAsync<T>(CancellationToken cancellationToken = default) where T : class, IUINode
Task ClearCacheAsync(CancellationToken cancellationToken = default)
```

ClearCache 只清理已关闭缓存，不关闭活跃或隐藏窗口；Context 同名方法清理整个上下文。见[关闭后缓存](../manual/resources.md#关闭后缓存)。

### `PanelContainerNode`

独立窗口；隐藏保留，关闭按注册策略销毁或缓存；没有返回历史。场景与代码见 [Panel 指南](../manual/panels.md)。

```csharp
Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode
bool TryGet<T>(out T? node) where T : class, IUINode
Task HideAsync(IUINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task ShowAsync(IUINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task CloseAsync(IUINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `StackContainerNode`

单窗口历史；隐藏被覆盖窗口，返回恢复原实例；最后一项可 Pop 至空栈。场景与代码见 [Stack 指南](../manual/stacks.md)。

```csharp
IReadOnlyList<IUINode> History { get; }
IUINode? Current { get; }
Task<T> PushAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode
Task PopAsync(bool playAnimation = true, CancellationToken cancellationToken = default)
Task PopToAsync(IUINode target, bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `TabContainerNode`

绑定内嵌页面并选择显示项，不记录切换历史。资源结构与代码见 [Tab 指南](../manual/tabs.md)。

```csharp
IReadOnlyDictionary<string, IUINode> Items { get; }
string? CurrentName { get; }
IUINode? Current { get; }
Task BindAsync(string name, IUINode node, IUIElement? embeddedElement = null, CancellationToken cancellationToken = default)
Task SwitchAsync(string name, bool playAnimation = true, CancellationToken cancellationToken = default)
Task SwitchAsync<T>(bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode
Task HideAsync(bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `ModalContainerNode`

叠层弹窗；下层保持可见，只有顶层接受输入。单窗口多级菜单与结果等待见 [Modal 指南](../manual/modals-and-dialogs.md)。

```csharp
IUINode? Current { get; }
ModalBackdropMode BackdropMode { get; }
IUIBackdrop? CurrentBackdrop { get; }
void ConfigureBackdrop(IUIBackdropFactory factory, ModalBackdropMode mode = ModalBackdropMode.Shared)
void DisableBackdrop()
void UseDefaultBackdrop(ModalBackdropMode mode = ModalBackdropMode.Shared)
Task<bool> RequestDismissAsync(CancellationToken token = default)
Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode
Task CloseAsync(IUINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task CloseToAsync(IUINode target, bool playAnimation = true, CancellationToken cancellationToken = default)
Task CloseCurrentAsync(bool playAnimation = true, CancellationToken cancellationToken = default)
Task<TResult> ShowDialogAsync<T, TResult>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IDialogNode<TResult>
```

`CloseCurrentAsync` 关闭并销毁当前顶层，恢复下层的输入与焦点；最后一层关闭后容器为空。没有打开的弹窗时抛出 `InvalidOperationException`。它沿用 `CloseAsync` 的转场、取消、结果等待和清理行为。

### `DialogNode<TResult>`

提交一次答案；等待方在关闭清理后取得结果。

```csharp
void Complete(TResult result)
```

## Lifecycle

IsFirstEnter 由 HasEntered 决定。Reason 区分实际操作原因，Source／Target 可能为 null。StateChangeContext 的 Previous／Current 是有效状态，非局部控件开关。

```csharp
LifecycleContext(IUINode node, ContainerNode? container, LifecycleReason reason, IUINode? source = null, IUINode? target = null)
// Properties: Node, Container, Reason, Source, Target, IsFirstEnter, IsCacheReuse (all get-only).
StateChangeContext(LifecycleContext operation, bool previous, bool current)
// Properties: Operation, Node, Previous, Current (all get-only).
// INodeLifecycle; NodeLifecycle provides virtual no-op implementations:
Task InitializeAsync(IUINode node, CancellationToken token)
Task EnteringAsync(LifecycleContext context, CancellationToken token)
void Entered(LifecycleContext context)
Task ExitingAsync(LifecycleContext context, CancellationToken token)
void Exited(LifecycleContext context)
void Activated(StateChangeContext context)
void Deactivated(StateChangeContext context)
void VisibilityChanged(StateChangeContext context)
Task DisposeAsync(IUINode node)
```

### 可选节点模块

模块同步准备来源，业务初始化结束后等待绑定准备，再将节点设为 Ready。异步集合等模块任务可通过初始化后阶段阻止未完成窗口显示。

```csharp
void UIContext.AddNodeExtension(IUINodeExtension extension)
// IUINodeExtension:
void BeforeInitialize(UINode node)
Task AfterInitializeAsync(UINode node, CancellationToken token)
ValueTask DisposeAsync(UINode node)
```

## Transitions / resources / failures

Select 同步只读；每次 Create 产生执行对象，成功创建执行对象后，Restore 总会执行。进度单调 0..1。UIResourceLease.Dispose 同步归还持有；附加失败不包装主异常。ConsoleUILog.Exception(Exception exception, string context) 可用作基础控制台日志。

```csharp
IUITransition Transitions.Select(Func<TransitionContext, IUITransition> select)
IUITransitionRun IUITransition.Create(TransitionContext context)
Task IUITransitionRun.PlayAsync(CancellationToken cancellationToken)
void IUITransitionRun.Restore()
TransitionContext(LifecycleContext lifecycle, bool entering, TransitionDirection direction)
// Properties: Lifecycle, Entering, Direction, Phase, Node, Element, Container, Source, Target, Reason.
void TransitionContext.ReportProgress(float progress)
// TransitionState properties: Context, Stage, Progress, IsRunning.
IUITransitionRun InstantTransition.Create(TransitionContext context)
Task InstantTransition.PlayAsync(CancellationToken cancellationToken)
void InstantTransition.Restore()
Task<IUIBackdrop> IUIBackdropFactory.CreateAsync(IUIElement? mount, Func<Task> requestDismiss, CancellationToken token)
Task IUIBackdrop.ShowAsync(bool playAnimation, CancellationToken token)
Task IUIBackdrop.HideAsync(bool playAnimation, CancellationToken token)
void IUIBackdrop.Restore(bool visible) // Also implements IUIElement.
void UIResourceLease.Dispose()
IReadOnlyList<Exception> UIFailure.GetSecondaryExceptions(Exception exception)
```

## Backend contracts

Loader 管真实资源，Element 管引擎显示实例；owned／borrowed 由适配实现。

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

## Enums / attributes

这些枚举不定义 HUD／Overlay 等项目层级。UIResource 声明生成器使用的资源路径；手写 Register 需明确传入路径。

```csharp
enum NodeState { Detached, Initializing, Ready, Disposing, Disposed }
enum WindowInstanceMode { Single, Multiple }
enum NodeCacheMode { None, KeepAlive }
enum ModalBackdropMode { Shared, PerModal }
enum TransitionDirection { None, Forward, Backward }
enum TransitionPhase { Enter, Exit, PushEnter, PushExit, PopEnter, PopExit }
enum TransitionStage { Preparing, Playing, Completed, Canceled, Failed }
enum LifecycleReason { Initialization, Open, Show, Hide, Push, Covered, Switch, Return, Close, Modal, Ancestor, Disposal }
UIResourceAttribute(string resource)
string UIResourceAttribute.Resource { get; }
WindowInstanceMode UIResourceAttribute.WindowMode { get; set; } // default Single.
NodeCacheMode UIResourceAttribute.CacheMode { get; set; } // default None.
const string UIFailure.SecondaryExceptionsKey = "UIFramework.SecondaryExceptions"
```

[Manual](../manual/concepts.md) · [Godot API](godot.md) · [Unity API](unity.md)
