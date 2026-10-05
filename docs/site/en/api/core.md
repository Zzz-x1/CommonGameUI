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
UINode? InputOwner { get; } // Set by the framework.
IUITransition DefaultTransition { get; set; }
void Register<T>(string? resource = null, WindowInstanceMode windowMode = WindowInstanceMode.Single) where T : UINode, new()
void Register<T>(Func<T> factory, string? resource = null, WindowInstanceMode windowMode = WindowInstanceMode.Single) where T : UINode
Task<UIResourceLease> PreloadAsync<T>(CancellationToken cancellationToken = default) where T : UINode
ValueTask DisposeAsync()
```

## UINode / `UINode<TArgs>`

Ordinary nodes start hidden. Context is unavailable before attach. Framework controls state properties; null Transition inherits. AddLifecycle is pre-attach only. RemoveLifecycle can unsubscribe while idle and Ready, but initialized behaviors still receive disposal. Attach does not reparent.

```csharp
Guid Id { get; }
string Name { get; }
UINode? Parent { get; }
IReadOnlyList<UINode> Children { get; }
UIContext Context { get; }
IUIElement? Element { get; }
NodeState State { get; }
bool HasEntered { get; }
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
Task AttachAsync(UINode parent, string name, IUIElement? element = null, CancellationToken cancellationToken = default)
UINode GetChild(string name)
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
protected Task FinishCloseAsync(UINode node, Action notify)
protected Task PrepareChangeAsync(LifecycleContext? exit, LifecycleContext? enter, CancellationToken token, bool playAnimation, TransitionDirection direction = TransitionDirection.None)
```

</details>

## Containers and result dialogs

Panel Single retains hidden items; Close disposes. Stack retains history; Pop disposes. Tab retains bound controls. Modal allows top-layer input. PopTo/CloseTo take earlier instances. ShowDialog waits for answer and cleanup; unanswered external closure cancels. RequestDismiss returns false when no handler is configured.

### `ContainerNode : UINode`

Default child transition.

```csharp
IUITransition? ChildTransition { get; set; }
```

### `PanelContainerNode`

Independent windows; hide retains, close disposes.

```csharp
Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : UINode
bool TryGet<T>(out T? node) where T : UINode
Task HideAsync(UINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task ShowAsync(UINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task CloseAsync(UINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `StackContainerNode`

Page history; return restores retained instances.

```csharp
IReadOnlyList<UINode> History { get; }
UINode? Current { get; }
Task<T> PushAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : UINode
Task PopAsync(bool playAnimation = true, CancellationToken cancellationToken = default)
Task PopToAsync(UINode target, bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `TabContainerNode`

Bind embedded pages and select one.

```csharp
IReadOnlyDictionary<string, UINode> Items { get; }
string? CurrentName { get; }
UINode? Current { get; }
Task BindAsync(string name, UINode node, IUIElement? embeddedElement = null, CancellationToken cancellationToken = default)
Task SwitchAsync(string name, bool playAnimation = true, CancellationToken cancellationToken = default)
Task SwitchAsync<T>(bool playAnimation = true, CancellationToken cancellationToken = default) where T : UINode
Task HideAsync(bool playAnimation = true, CancellationToken cancellationToken = default)
```

### `ModalContainerNode`

Nested modals; only the top receives input.

```csharp
UINode? Current { get; }
ModalBackdropMode BackdropMode { get; }
IUIBackdrop? CurrentBackdrop { get; }
void ConfigureBackdrop(IUIBackdropFactory factory, ModalBackdropMode mode = ModalBackdropMode.Shared)
Task<bool> RequestDismissAsync(CancellationToken token = default)
Task<T> OpenAsync<T>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : UINode
Task CloseAsync(UINode node, bool playAnimation = true, CancellationToken cancellationToken = default)
Task CloseToAsync(UINode target, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<TResult> ShowDialogAsync<T, TResult>(object? args = null, bool playAnimation = true, CancellationToken cancellationToken = default) where T : DialogNode<TResult>
```

### `DialogNode<TResult>`

Answer once; the caller receives it after closure/cleanup.

```csharp
void Complete(TResult result)
```

## Lifecycle

IsFirstEnter follows HasEntered. Reason describes the operation; Source/Target can be null. Previous/Current are effective state, not local widget switches.

```csharp
LifecycleContext(UINode node, ContainerNode? container, LifecycleReason reason, UINode? source = null, UINode? target = null)
// Properties: Node, Container, Reason, Source, Target, IsFirstEnter (all get-only).
StateChangeContext(LifecycleContext operation, bool previous, bool current)
// Properties: Operation, Node, Previous, Current (all get-only).
// INodeLifecycle; NodeLifecycle provides virtual no-op implementations:
Task InitializeAsync(UINode node, CancellationToken token)
Task EnteringAsync(LifecycleContext context, CancellationToken token)
void Entered(LifecycleContext context)
Task ExitingAsync(LifecycleContext context, CancellationToken token)
void Exited(LifecycleContext context)
void Activated(StateChangeContext context)
void Deactivated(StateChangeContext context)
void VisibilityChanged(StateChangeContext context)
Task DisposeAsync(UINode node)
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
enum ModalBackdropMode { Shared, PerModal }
enum TransitionDirection { None, Forward, Backward }
enum TransitionPhase { Enter, Exit, PushEnter, PushExit, PopEnter, PopExit }
enum TransitionStage { Preparing, Playing, Completed, Canceled, Failed }
enum LifecycleReason { Initialization, Open, Show, Hide, Push, Covered, Switch, Return, Close, Modal, Ancestor, Disposal }
UIResourceAttribute(string resource)
string UIResourceAttribute.Resource { get; }
WindowInstanceMode UIResourceAttribute.WindowMode { get; set; } // default Single.
const string UIFailure.SecondaryExceptionsKey = "UIFramework.SecondaryExceptions"
```

[Manual](../manual/concepts.md) · [Godot API](godot.md) · [Unity API](unity.md)
