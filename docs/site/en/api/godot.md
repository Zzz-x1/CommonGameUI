# Godot API Reference

Namespace UIFramework.Godot; engine types come from that engine. Signatures omit implementation bodies.

## Native business nodes

Attach the window script to the resource root and inherit one of these bases. Containers return that script instance after loading. Shared lifecycle and transition contracts are in [Core API](core.md).

```csharp
// UIFramework.Godot
abstract class UINode : Control, IUINode
abstract class UINode<TArgs> : UINode, IParameterizedNode<TArgs>
abstract class DialogNode<TResult> : UINode, IDialogNode<TResult>
string UIName { get; }
bool IsUIVisible { get; }
protected void Observe(Task task)
void Complete(TResult result) // DialogNode<TResult>
Task<TResult> WaitForResultAsync(CancellationToken token) // DialogNode<TResult>
```

## Setup and control access

```csharp
UIContext UI.Create(global::Godot.Node owner, IUIResourceLoader? loader = null, IUILog? logger = null, Control? rootMount = null)
// UIExtensions:
Task AttachToAsync(this UINode node, IUINode parent, string name, CancellationToken token = default)
Task BindAsync(this TabContainerNode tabs, string name, UINode node, CancellationToken token = default)
Task AttachToAsync(this IUINode node, IUINode parent, string name, Control control, CancellationToken token = default)
Task<T> CreateContainerAsync<T>(this IUINode parent, string name, Control? mount = null, CancellationToken token = default) where T : ContainerNode, new()
Task BindAsync(this TabContainerNode tabs, string name, Control page, CancellationToken token = default)
Task BindAsync(this TabContainerNode tabs, string name, IUINode node, Control page, CancellationToken token = default)
Control GetRoot(this IUINode node)
T GetControl<T>(this IUINode node, string path) where T : Control
```

## Lifetime bindings

Synchronously bind an attached node. Engine destruction starts cleanup. Dispose disconnects only; Completion waits for cleanup. See [Lifecycle](../manual/lifecycle.md).

```csharp
// LifetimeExtensions:
LifetimeBinding BindLifetime(this IUINode node, global::Godot.Node owner)
LifetimeBinding BindLifetime(this UIContext ui, global::Godot.Node owner)
// LifetimeBinding:
Task Completion { get; }
void Dispose()
```

## Transitions and backdrops

```csharp
IUITransition Transitions.Fade(double duration = 0.15, string? targetPath = null, double delay = 0, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition Transitions.SlideHorizontal(double duration = 0.20, string? targetPath = null, double delay = 0, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition Transitions.ScaleFade(double duration = 0.16, float fromScale = 0.94f, string? targetPath = null, double delay = 0, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition Transitions.Simple(TransitionFrame from, TransitionFrame to, double duration = 0.20, double delay = 0, string? targetPath = null, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition Transitions.Animation(string playerPath, string enterAnimation, string exitAnimation, Action<Control> restore)
IUIBackdropFactory Backdrops.Dim(Action<Task> observe, Color? color = null, double duration = 0.12)
// TransitionFrame is a sealed record with init properties:
Vector2 Offset { get; init; } // default Zero
Vector2 RelativeOffset { get; init; } // default Zero
Vector2 Scale { get; init; } // default One
float Alpha { get; init; } // default 1
```

## Low-level adapter types

```csharp
UIHost(Viewport viewport, Control? rootMount = null)
Control? RootMount { get; }
void UIHost.VerifyAccess()
Task UIHost.NextFrameAsync()
object? UIHost.CaptureFocus()
void UIHost.Focus(IUIElement? element)
void UIHost.RestoreFocus(object? focus)
void UIHost.Error(string message)
void UIHost.Exception(Exception exception, string context)
UIResourceLoader(UIHost host)
Task<IUIResource> UIResourceLoader.LoadAsync(string resource, CancellationToken token)
UIElement(Control control, bool owned = false)
Control UIElement.Control { get; }
T UIElement.Get<T>(string path) where T : global::Godot.Node
void UIElement.SetVisible(bool visible)
void UIElement.SetInteractive(bool interactive)
void UIElement.BringToFront()
object UIElement.CaptureOrder()
void UIElement.RestoreOrder(object order)
ValueTask UIElement.DisposeAsync()
```

## Parameter notes

- Create requires owner in the scene tree; defaults are UIResourceLoader and Host logging.
- The default `UIResourceLoader.LoadAsync` calls `ResourceLoader.Load` synchronously on the scene thread and returns a completed task; loading cannot be interrupted, and cancellation is checked afterward.
- targetPath is relative to the root; null means root. Attach/bind borrow Controls.
- Simple plays from→to; Select chooses direction branches. Offset uses pixels, RelativeOffset uses Size, and Scale/Alpha multiply authored values.
- duration/delay are finite and nonnegative; fromScale is positive; Alpha is within 0..1. Delay counts toward progress.
- Animation is non-looping; restore resets tracks. Dim observe must inspect returned tasks.

[Integration](../integrations/godot.md) · [Transitions](../manual/transitions.md)
