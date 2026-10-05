# Godot API Reference

Namespace UIFramework.Godot; engine types come from that engine. Signatures omit implementation bodies.

## Setup and control access

```csharp
UIContext GodotUI.Create(global::Godot.Node owner, IUIResourceLoader? loader = null, IUILog? logger = null)
// GodotUIExtensions:
Task AttachToAsync(this UINode node, UINode parent, string name, Control control, CancellationToken token = default)
Task<T> CreateContainerAsync<T>(this UINode parent, string name, Control mount, CancellationToken token = default) where T : ContainerNode, new()
Task BindAsync(this TabContainerNode tabs, string name, Control page, CancellationToken token = default)
Task BindAsync(this TabContainerNode tabs, string name, UINode node, Control page, CancellationToken token = default)
Control GetControl(this UINode node)
T GetControl<T>(this UINode node, string path) where T : Control
```

## Transitions and backdrops

```csharp
IUITransition GodotTransitions.Fade(double duration = 0.15, string? targetPath = null, double delay = 0, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition GodotTransitions.SlideHorizontal(double duration = 0.20, string? targetPath = null, double delay = 0, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition GodotTransitions.ScaleFade(double duration = 0.16, float fromScale = 0.94f, string? targetPath = null, double delay = 0, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition GodotTransitions.Simple(GodotTransitionFrame from, GodotTransitionFrame to, double duration = 0.20, double delay = 0, string? targetPath = null, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
IUITransition GodotTransitions.Animation(string playerPath, string enterAnimation, string exitAnimation, Action<Control> restore)
IUIBackdropFactory GodotBackdrops.Dim(Action<Task> observe, Color? color = null, double duration = 0.12)
// GodotTransitionFrame is a sealed record with init properties:
Vector2 Offset { get; init; } // default Zero
Vector2 RelativeOffset { get; init; } // default Zero
Vector2 Scale { get; init; } // default One
float Alpha { get; init; } // default 1
```

## Low-level adapter types

```csharp
GodotUIHost(Viewport viewport)
void GodotUIHost.VerifyAccess()
object? GodotUIHost.CaptureFocus()
void GodotUIHost.Focus(IUIElement? element)
void GodotUIHost.RestoreFocus(object? focus)
void GodotUIHost.Error(Exception exception, string context)
GodotSceneLoader(GodotUIHost host)
Task<IUIResource> GodotSceneLoader.LoadAsync(string resource, CancellationToken token)
GodotUIElement(Control control, bool owned = false)
Control GodotUIElement.Control { get; }
T GodotUIElement.Get<T>(string path) where T : global::Godot.Node
void GodotUIElement.SetVisible(bool visible)
void GodotUIElement.SetInteractive(bool interactive)
void GodotUIElement.BringToFront()
object GodotUIElement.CaptureOrder()
void GodotUIElement.RestoreOrder(object order)
ValueTask GodotUIElement.DisposeAsync()
```

## Parameter notes

- Create requires owner in the scene tree; defaults are GodotSceneLoader and Host logging.
- targetPath is relative to the root; null means root. Attach/bind borrow Controls.
- Simple plays from→to; Select chooses direction branches. Offset uses pixels, RelativeOffset uses Size, and Scale/Alpha multiply authored values.
- duration/delay are finite and nonnegative; fromScale is positive; Alpha is within 0..1. Delay counts toward progress.
- Animation is non-looping; restore resets tracks. Dim observe must inspect returned tasks.

[Integration](../integrations/godot.md) · [Transitions](../manual/transitions.md)
