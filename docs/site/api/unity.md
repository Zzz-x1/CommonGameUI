# Unity API Reference

命名空间 UIFramework.Unity；引擎类型来自对应引擎。以下为调用签名，省略方法体。

## 原生业务节点

窗口脚本挂载在资源根节点，继承以下基类；容器打开资源后返回该脚本实例。共有生命周期与转场契约见 [Core API](core.md)。

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

## 初始化与控件访问

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

## 生命周期绑定

同步绑定已挂载节点；销毁引擎对象后自动清理。Dispose 只解除绑定；Completion 等待清理。详见[生命周期](../manual/lifecycle.md)。

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

## 转场与遮罩

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

## 底层适配类型

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

## 参数要点

- Create 要求有效 Canvas／EventSystem，配置帧泵，默认使用 Resources loader。
- targetPath 相对根；null 表示根。挂载／绑定借用 RectTransform。
- Simple 使用 from→to 和 AnimationCurve，默认 EaseInOut。Offset 为像素，RelativeOffset 按矩形尺寸；Scale／Alpha 乘原值。
- ScaleFade 使用 .85 起始缩放；没有 delay 参数。
- Animation 使用非循环 legacy Clip 和未缩放时间；playerPath 可为空字符串表示根。restore 恢复全部轨道。
- Dim 的 observe 必须观察返回任务。验证状态见[Unity 适配](../integrations/unity.md)。

Timeline 路径相对节点根，null 表示根 Director。要求组件启用、资产时长有限、关闭 Play On Awake，且没有已运行的播放图。restore 必须恢复所有轨道控制的属性。

[适配说明](../integrations/unity.md) · [时间轴](../manual/timeline-animation.md)
