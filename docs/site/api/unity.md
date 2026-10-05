# Unity API Reference

命名空间 UIFramework.Unity；引擎类型来自对应引擎。以下为调用签名，省略方法体。

## 初始化与控件访问

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

## 生命周期绑定

同步绑定已挂载节点；销毁引擎对象后自动清理。Dispose 只解除绑定；Completion 等待清理。详见[生命周期](../manual/lifecycle.md)。

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

## 转场与遮罩

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

## 底层适配类型

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

## 参数要点

- Create 要求有效 Canvas／EventSystem，配置帧泵，默认使用 Resources loader。
- targetPath 相对根；null 表示根。挂载／绑定借用 RectTransform。
- Simple 使用 from→to 和 AnimationCurve，默认 EaseInOut。Offset 为像素，RelativeOffset 按矩形尺寸；Scale／Alpha 乘原值。
- ScaleFade 使用 .85 起始缩放；没有 delay 参数。
- Animation 使用非循环 legacy Clip 和未缩放时间；playerPath 可为空字符串表示根。restore 恢复全部轨道。
- Dim 的 observe 必须观察返回任务。验证状态见[Unity 适配](../integrations/unity.md)。

[适配说明](../integrations/unity.md) · [时间轴](../manual/timeline-animation.md)
