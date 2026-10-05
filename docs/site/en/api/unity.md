# Unity API Reference

Namespace UIFramework.Unity; engine types come from that engine. Signatures omit implementation bodies.

## Setup / extensions

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

## Transitions / backdrop

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

## Host / resource / element

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

## Parameter semantics and status

Create configures the Canvas frame pump and defaults to Resources loading and Host logging; Canvas/EventSystem must be valid. Attach extensions borrow RectTransforms. Null targetPath means root. Unity signatures retain the current source form without nullable annotations.

Simple uses explicit frames and AnimationCurve, default EaseInOut. Scale is scalar; Offset uses pixels, RelativeOffset uses rect size, and Alpha/Scale multiply authored values. There are no Godot-style delay/Tween enum parameters. ScaleFade uses .85 scale. Dim defaults to (.04, .07, .12, .7), and observe must inspect tasks.

Animation playerPath resolves the RectTransform containing legacy Animation; an empty string targets the root. enterClip/exitClip name non-looping Clips registered on that component. restore restores all track presentation. Playback uses unscaled time and works with timeScale=0.

Latest backdrop/first-frame fixes have compilation only; native runtime and Demo timeline asset verification remain pending.

[Integration](../integrations/unity.md) · [Timelines](../manual/timeline-animation.md)
