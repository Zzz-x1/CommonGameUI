# Unity API Reference

Namespace UIFramework.Unity; engine types come from that engine. Signatures omit implementation bodies.

## Native business nodes

Attach the window script to the resource root and inherit one of these bases. Containers return that script instance after loading. Shared lifecycle and transition contracts are in [Core API](core.md).

```csharp
// UIFramework.Unity
abstract class UINode : MonoBehaviour, IUINode
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
UIContext UI.Create(Canvas canvas, EventSystem events, IUIResourceLoader loader = null, IUILog logger = null)
// UIExtensions:
Task AttachToAsync(this UINode node, IUINode parent, string name, CancellationToken token = default)
Task BindAsync(this TabContainerNode tabs, string name, UINode node, CancellationToken token = default)
Task AttachToAsync(this IUINode node, IUINode parent, string name, RectTransform rect, CancellationToken token = default)
Task<T> CreateContainerAsync<T>(this IUINode parent, string name, RectTransform mount = null, CancellationToken token = default) where T : ContainerNode, new()
Task BindAsync(this TabContainerNode tabs, string name, RectTransform page, CancellationToken token = default)
Task BindAsync(this TabContainerNode tabs, string name, IUINode node, RectTransform page, CancellationToken token = default)
RectTransform GetRoot(this IUINode node)
T GetControl<T>(this IUINode node, string path) where T : Component
```

## Lifetime bindings

Synchronously bind an attached node. Engine destruction starts cleanup. Dispose disconnects only; Completion waits for cleanup. See [Lifecycle](../manual/lifecycle.md).

```csharp
// LifetimeExtensions:
LifetimeBinding BindLifetime(this IUINode node, GameObject owner)
LifetimeBinding BindLifetime(this IUINode node, UnityEngine.SceneManagement.Scene scene)
LifetimeBinding BindLifetime(this UIContext ui, GameObject owner)
LifetimeBinding BindLifetime(this UIContext ui, UnityEngine.SceneManagement.Scene scene)
// LifetimeBinding:
Task Completion { get; }
void Dispose()
```

## Transitions and backdrops

```csharp
IUITransition Transitions.Fade(double duration = .18, string targetPath = null)
IUITransition Transitions.ScaleFade(double duration = .22, string targetPath = null)
IUITransition Transitions.SlideHorizontal(double duration = .28, string targetPath = null)
IUITransition Transitions.Simple(TransitionFrame from, TransitionFrame to, double duration = .25, string targetPath = null, AnimationCurve easing = null)
IUITransition Transitions.Animation(string playerPath, string enterClip, string exitClip, Action<RectTransform> restore)
IUITransition Transitions.Timeline(string enterDirectorPath, string exitDirectorPath, Action<RectTransform> restore)
IUIBackdropFactory Backdrops.Dim(Action<Task> observe, Color? color = null, double duration = .15)
TransitionFrame(Vector2 offset = default, Vector2 relativeOffset = default, float scale = 1, float alpha = 1)
// Frame get-only properties: Vector2 Offset, Vector2 RelativeOffset, float Scale, float Alpha.
```

## Low-level adapter types

```csharp
UIHost(EventSystem events, FramePump frames, RectTransform rootMount = null)
RectTransform RootMount { get; }
void UIHost.VerifyAccess()
Task UIHost.NextFrameAsync()
object UIHost.CaptureFocus()
void UIHost.Focus(IUIElement element)
void UIHost.RestoreFocus(object focus)
void UIHost.Error(Exception exception, string context)
// FramePump : MonoBehaviour
Task FramePump.NextFrameAsync()
UIResourceLoader(UIHost host)
Task<IUIResource> UIResourceLoader.LoadAsync(string resource, CancellationToken token)
UIElement(UIHost host, RectTransform rect, bool owned = false)
RectTransform UIElement.Rect { get; }
UIHost UIElement.Host { get; }
void UIElement.SetVisible(bool visible)
void UIElement.SetInteractive(bool interactive)
void UIElement.BringToFront()
object UIElement.CaptureOrder()
void UIElement.RestoreOrder(object order)
ValueTask UIElement.DisposeAsync()
```

## Parameter notes

- Create requires valid Canvas/EventSystem, configures a frame pump and defaults to Resources loading.
- targetPath is relative to the root; null means root. Attach/bind borrow RectTransforms.
- Simple uses from→to and AnimationCurve, default EaseInOut. Offset uses pixels, RelativeOffset uses rect size, and Scale/Alpha multiply authored values.
- ScaleFade starts at .85 scale; there is no delay parameter.
- Animation uses non-looping legacy Clips and unscaled time. Empty playerPath means root; restore resets all tracks.
- Dim observe must inspect returned tasks. See [Unity integration](../integrations/unity.md) for status.

Timeline paths are relative to the node root; null selects the root director. Directors require an enabled component, an asset with finite duration, Play On Awake disabled, and no existing graph. Restore must reset every property owned by the tracks.

[Integration](../integrations/unity.md) · [Timelines](../manual/timeline-animation.md)
