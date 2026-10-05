# 转场与条件分支

同一背包从大厅进入淡入，从商店进入横移，返回时使用短动画。

## 用法

优先级：节点 Transition → 直接容器 ChildTransition → Context.DefaultTransition。默认 Instant；容器配置不穿透后代容器。

```csharp
var fade = GodotTransitions.Fade(.15);
var slide = GodotTransitions.SlideHorizontal(.20, targetPath: "Content");
ui.DefaultTransition = fade;
pages.ChildTransition = Transitions.Select(context =>
    context.Direction == TransitionDirection.Backward ? fade :
    context.Source is ShopView ? slide : fade);
var inventory = await pages.PushAsync<InventoryView>();
// The ordering/state properties are available on every node:
inventory.TransitionOrder = 10;
inventory.TransitionChanged += state => decoration.Update(state.Progress);
await pages.PopAsync(playAnimation: false);
```

## 要点

- Select 同步按 Phase、Source／Target、Direction 或业务设置选择配方，不修改框架。
- playAnimation=false 只跳过动画，资源、生命周期与清理仍执行。
- TransitionOrder 高值临时画在上方，结束恢复。
- 进度表示动画时间；整个操作是否成功以 await 为准。缓动参数见各引擎 API。

[时间轴](timeline-animation.md) · [Godot API](../api/godot.md) · [Unity API](../api/unity.md)
