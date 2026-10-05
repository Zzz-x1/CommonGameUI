# Unity API Reference

命名空间 UIFramework.Unity；引擎类型来自对应引擎。以下为调用签名，省略方法体。

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

## 参数语义与状态

Create 配置 Canvas 上的帧泵，默认 Resources loader 和 Host 日志；Canvas 与 EventSystem 必须有效。扩展挂载借用 RectTransform；targetPath=null 为根。Unity 签名保留当前源码未启用 Nullable 标注的形式。

Simple 使用显式起止帧和 AnimationCurve，默认 EaseInOut；Scale 是标量，Offset 为像素，RelativeOffset 按矩形尺寸，Alpha／Scale 乘原值。没有 Godot 版 delay／Tween 枚举参数。ScaleFade 固定从 .85 缩放入／出。Dim 默认颜色 (.04, .07, .12, .7)，observe 必须观察任务。

Animation 的 playerPath 对应持有 legacy Animation 的 RectTransform，根可用空字符串；enterClip／exitClip 为组件中的非循环 Clip 名称。restore 恢复所有轨道表现。播放使用未缩放时间，即使 timeScale=0 也可推进。

最新遮罩与首帧修复仅编译验证；原生运行与 Demo 时间轴资产验证待完成。

[适配说明](../integrations/unity.md) · [时间轴](../manual/timeline-animation.md)
