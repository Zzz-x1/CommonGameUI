# Godot API Reference

命名空间 UIFramework.Godot；引擎类型来自对应引擎。以下为调用签名，省略方法体。

## 原生业务节点

窗口脚本挂载在资源根节点，继承以下基类；容器打开资源后返回该脚本实例。共有生命周期与转场契约见 [Core API](core.md)。

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

## 初始化与控件访问

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

## 生命周期绑定

同步绑定已挂载节点；销毁引擎对象后自动清理。Dispose 只解除绑定；Completion 等待清理。详见[生命周期](../manual/lifecycle.md)。

```csharp
// LifetimeExtensions:
LifetimeBinding BindLifetime(this IUINode node, global::Godot.Node owner)
LifetimeBinding BindLifetime(this UIContext ui, global::Godot.Node owner)
// LifetimeBinding:
Task Completion { get; }
void Dispose()
```

## 转场与遮罩

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

## 底层适配类型

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

## 参数要点

- Create 要求 owner 已进入场景树；默认使用 UIResourceLoader 和 Host 日志。
- 默认 `UIResourceLoader.LoadAsync` 在场景线程同步调用 `ResourceLoader.Load`，返回已完成任务；加载无法中断，完成后检查取消令牌。
- targetPath 相对资源根；null 表示根。挂载／绑定借用 Control。
- Simple 按 from→to 播放；方向分支由 Select 选择。Offset 为像素，RelativeOffset 按 Size；Scale／Alpha 乘原值。
- duration／delay 有限非负；fromScale 为正，Alpha 在 0..1。delay 计入进度。
- Animation 非循环，restore 恢复轨道属性。Dim 的 observe 必须观察返回任务。

[适配说明](../integrations/godot.md) · [转场](../manual/transitions.md)
