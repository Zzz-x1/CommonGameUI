# Core API Reference

Namespace: UIFramework. Signatures and defaults are listed below; see the [Manual](../manual/concepts.md) for usage.

## UIContext

Root is structural. Register is synchronous and rejects duplicate types. Null resource means a resource-less node. The generator passes attribute paths; handwritten registration must supply them explicitly. DefaultTransition starts as Instant and rejects null. Repeated disposal shares its outcome.

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

## Context module extensions

Optional modules register a factory once. Context initializes them and disposes them in reverse order after Root and resource cleanup. Ordinary UI code does not call these interfaces.

```csharp
interface IUIContextExtension
{
    void Initialize(UIContext context);
    ValueTask DisposeAsync();
}
void UIContextExtensions.Register(Func<IUIContextExtension> factory);
```

## Shared node identity

Core structural nodes and native business scripts implement `IUINode`. Container results, Children, History, Current, input ownership and lifecycle descriptions preserve business identity. Interface Name / IsVisible map to native UIName / IsUIVisible. Business code normally does not need the backing Core Node.

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

Ordinary nodes start hidden. Context is unavailable before attach. Framework controls state properties; null Transition inherits. AddLifecycle is pre-attach only. RemoveLifecycle can unsubscribe while idle and Ready, but initialized behaviors still receive disposal. Attach does not reparent.

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

Override in lifecycle order. Post-commit notifications are read-only toward the framework. OnApplyState and child-removal hooks extend presentation/container cleanup; ordinary business should favor standard lifecycle.

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
<summary>Node and custom-container extension hooks</summary>

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


## Containers and result dialogs

Panel Single retains hidden items and Stack retains history; Close/Pop destroy by default or cache with KeepAlive. Tab retains bound controls. Modal allows top-layer input. PopTo/CloseTo take live instances in their container; the current target completes without changes. ShowDialog waits for answer and cleanup; unanswered external closure cancels. RequestDismiss returns false when no handler is configured.

### `ContainerNode : UINode`

Default child transition.

```csharp
IUITransition? ChildTransition { get; set; }
Task ClearCacheAsync<T>(CancellationToken cancellationToken = default) where T : class, IUINode
Task ClearCacheAsync(CancellationToken cancellationToken = default)
```

ClearCache releases closed cached instances without closing open or hidden windows; the Context method clears caches across the context. See [closed-instance caching](../manual/resources.md#closed-instance-caching).

### `PanelContainerNode`

Independent windows; hide retains, while close destroys or caches according to registration. No return history. See the [Panel guide](../manual/panels.md) for scenarios and code.

```csharp
Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode
bool TryGet<T>(out T? node) where T : class, IUINode
Task HideAsync(IUINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task ShowAsync(IUINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task CloseAsync(IUINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `StackContainerNode`

One current window with history; covered windows are hidden and Back restores their instances. Popping the last item leaves an empty stack. See the [Stack guide](../manual/stacks.md).

```csharp
IReadOnlyList<IUINode> History { get; }
IUINode? Current { get; }
Task<T> PushAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IUINode
Task PopAsync(bool playAnimation = true, CancellationToken cancellationToken = default)
Task PopToAsync(IUINode target, bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `TabContainerNode`

Bind embedded pages and select one without recording switching history. See the [Tab guide](../manual/tabs.md) for resource structure and code.

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

Layered modals; lower windows remain visible and only the top receives input. See the [Modal guide](../manual/modals-and-dialogs.md) for single-window menu history and result waiting.

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
Task<TResult> ShowDialogAsync<T, TResult>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : class, IDialogNode<TResult>
```

### `DialogNode<TResult>`

Answer once; the caller receives it after closure/cleanup.

```csharp
void Complete(TResult result)
```

## Lifecycle

IsFirstEnter follows HasEntered. Reason describes the operation; Source/Target can be null. Previous/Current are effective state, not local widget switches.

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

### Optional node modules

Modules prepare sources synchronously. After business initialization, binding preparation is awaited before setting the node to Ready. Asynchronous module work, such as initial collection creation, can delay display through this phase.

```csharp
void UIContext.AddNodeExtension(IUINodeExtension extension)
// IUINodeExtension:
void BeforeInitialize(UINode node)
Task AfterInitializeAsync(UINode node, CancellationToken token)
ValueTask DisposeAsync(UINode node)
```

## Transitions / resources / failures

Select is synchronous/read-only; Create produces a run and Restore always executes after a run is successfully created. Progress is monotonic 0..1. UIResourceLease.Dispose releases ownership synchronously. Secondary failures do not wrap the primary. ConsoleUILog.Error(Exception exception, string context) provides basic console logging.

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

Loader owns acquired resources, Element owns engine display management; adapters implement owned/borrowed semantics.

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

## Enums / attributes

These enums prescribe no HUD/Overlay layers. UIResource declares paths for the generator. Handwritten Register must pass the path explicitly.

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
