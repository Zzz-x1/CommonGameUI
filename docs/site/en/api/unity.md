# Unity API Reference

Namespace UIFramework.Unity; engine types come from that engine. Signatures omit implementation bodies.

## Setup and control access

```csharp
UIContext UnityUI.Create(Canvas canvas, EventSystem events, IUIResourceLoader loader = null, IUILog logger = null)
// UnityUIExtensions:
Task AttachToAsync(this UINode node, UINode parent, string name, RectTransform rect, CancellationToken token = default)
Task<T> CreateContainerAsync<T>(this UINode parent, string name, RectTransform mount, CancellationToken token = default) where T : ContainerNode, new()
Task BindAsync(this TabContainerNode tabs, string name, RectTransform page, CancellationToken token = default)
Task BindAsync(this TabContainerNode tabs, string name, UINode node, RectTransform page, CancellationToken token = default)
RectTransform GetRect(this UINode node)
T GetComponent<T>(this UINode node, string path) where T : Component
```

## Lifetime bindings

Synchronously bind an attached node. Engine destruction starts cleanup. Dispose disconnects only; Completion waits for cleanup. See [Lifecycle](../manual/lifecycle.md).

```csharp
// UnityLifetimeExtensions:
UnityLifetimeBinding BindLifetime(this UINode node, GameObject owner)
UnityLifetimeBinding BindLifetime(this UINode node, UnityEngine.SceneManagement.Scene scene)
UnityLifetimeBinding BindLifetime(this UIContext ui, GameObject owner)
UnityLifetimeBinding BindLifetime(this UIContext ui, UnityEngine.SceneManagement.Scene scene)
// UnityLifetimeBinding:
Task Completion { get; }
void Dispose()
```

## Transitions and backdrops

```csharp
IUITransition UnityTransitions.Fade(double duration = .18, string targetPath = null)
IUITransition UnityTransitions.ScaleFade(double duration = .22, string targetPath = null)
IUITransition UnityTransitions.SlideHorizontal(double duration = .28, string targetPath = null)
IUITransition UnityTransitions.Simple(UnityTransitionFrame from, UnityTransitionFrame to, double duration = .25, string targetPath = null, AnimationCurve easing = null)
IUITransition UnityTransitions.Animation(string playerPath, string enterClip, string exitClip, Action<RectTransform> restore)
IUIBackdropFactory UnityBackdrops.Dim(Action<Task> observe, Color? color = null, double duration = .15)
UnityTransitionFrame(Vector2 offset = default, Vector2 relativeOffset = default, float scale = 1, float alpha = 1)
// Frame get-only properties: Vector2 Offset, Vector2 RelativeOffset, float Scale, float Alpha.
```

## Low-level adapter types

```csharp
UnityUIHost(EventSystem events, UnityFramePump frames)
void UnityUIHost.VerifyAccess()
Task UnityUIHost.NextFrameAsync()
object UnityUIHost.CaptureFocus()
void UnityUIHost.Focus(IUIElement element)
void UnityUIHost.RestoreFocus(object focus)
void UnityUIHost.Error(Exception exception, string context)
// UnityFramePump : MonoBehaviour
Task UnityFramePump.NextFrameAsync()
UnityPrefabLoader(UnityUIHost host)
Task<IUIResource> UnityPrefabLoader.LoadAsync(string resource, CancellationToken token)
UnityUIElement(UnityUIHost host, RectTransform rect, bool owned = false)
RectTransform UnityUIElement.Rect { get; }
UnityUIHost UnityUIElement.Host { get; }
void UnityUIElement.SetVisible(bool visible)
void UnityUIElement.SetInteractive(bool interactive)
void UnityUIElement.BringToFront()
object UnityUIElement.CaptureOrder()
void UnityUIElement.RestoreOrder(object order)
ValueTask UnityUIElement.DisposeAsync()
```

## Parameter notes

- Create requires valid Canvas/EventSystem, configures a frame pump and defaults to Resources loading.
- targetPath is relative to the root; null means root. Attach/bind borrow RectTransforms.
- Simple uses from→to and AnimationCurve, default EaseInOut. Offset uses pixels, RelativeOffset uses rect size, and Scale/Alpha multiply authored values.
- ScaleFade starts at .85 scale; there is no delay parameter.
- Animation uses non-looping legacy Clips and unscaled time. Empty playerPath means root; restore resets all tracks.
- Dim observe must inspect returned tasks. See [Unity integration](../integrations/unity.md) for status.

[Integration](../integrations/unity.md) · [Timelines](../manual/timeline-animation.md)
