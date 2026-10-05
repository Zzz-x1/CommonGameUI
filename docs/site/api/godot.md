# Godot API Reference

命名空间 UIFramework.Godot；引擎类型来自对应引擎。以下为调用签名，省略方法体。

## Setup / extensions

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

## Transitions / backdrop

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

## Host / resource / element

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

## 参数语义

Create 在 owner 进入树后调用，默认使用 GodotSceneLoader 和 Host 日志。扩展挂载／绑定借用 Control，不销毁原场景。targetPath=null 使用根，其他路径相对根解析。

Simple 明确 from→to，不自动根据进入／退出反转；用 Transitions.Select 选择。Offset 是像素偏移，RelativeOffset 按实际 Size，Scale／Alpha 乘资源原值。delay 计入动画进度，缓动不改变进度时间。ScaleFade 的 fromScale 必须正数，帧 alpha 为有限 0..1；duration／delay 有限非负。

Animation 必須非循环，restore 恢复所有自定义轨道属性。Dim 默认颜色 (0.08, 0.14, 0.13, 0.68)，observe 必须观察失败。GodotUIHost 的帧等待是内部方法，不属于公开扩展 API。

[适配说明](../integrations/godot.md) · [转场](../manual/transitions.md)
