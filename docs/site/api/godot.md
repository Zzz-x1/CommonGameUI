# Godot API Reference

命名空间 UIFramework.Godot；引擎类型来自对应引擎。以下为调用签名，省略方法体。

## 初始化与控件访问

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

## 转场与遮罩

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

## 底层适配类型

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

## 参数要点

- Create 要求 owner 已进入场景树；默认使用 GodotSceneLoader 和 Host 日志。
- targetPath 相对资源根；null 表示根。挂载／绑定借用 Control。
- Simple 按 from→to 播放；方向分支由 Select 选择。Offset 为像素，RelativeOffset 按 Size；Scale／Alpha 乘原值。
- duration／delay 有限非负；fromScale 为正，Alpha 在 0..1。delay 计入进度。
- Animation 非循环，restore 恢复轨道属性。Dim 的 observe 必须观察返回任务。

[适配说明](../integrations/godot.md) · [转场](../manual/transitions.md)
