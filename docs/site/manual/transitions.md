# 转场与条件分支

## 使用场景

背包从大厅进入完整演出，从商店进入横移；返回时采用短反向动画。只按真实来源与阶段选择，不增加路由标签。

## 资源与职责

Godot／Unity 提供配方，节点 Transition 优先于直接容器 ChildTransition，最后使用 Context.DefaultTransition，初始 Instant。容器设置不穿透到后代容器。动画目标最好是布局内的独立内容包装层。

## 使用代码

```csharp
var fade = GodotTransitions.Fade(.15);
var slide = GodotTransitions.SlideHorizontal(.20, targetPath: "Content");
ui.DefaultTransition = fade;
pages.ChildTransition = Transitions.Select(context =>
    context.Direction == TransitionDirection.Backward ? fade :
    context.Source is ShopView ? slide : fade);
await pages.PushAsync<InventoryView>();
await pages.PopAsync(playAnimation: false);
// The ordering/state properties are available on every node:
inventory.TransitionOrder = 10;
inventory.TransitionChanged += state => decoration.Update(state.Progress);
```

## 行为与限制

Select 只接受一个同步 Func；可直接判断 Phase、Source／Target、Entering、Direction 和业务偏好。选择在准备前解析一次，不修改框架；抛错、null 或循环选择都报错，不降级。源退出与目标进入可以并行执行。playAnimation=false 跳过选择和播放，但资源、生命周期与清理仍等待。TransitionOrder 高值在两项转场时置于上方，结束恢复顺序。运行阶段为 Preparing／Playing／Completed／Canceled／Failed，进度描述动画时间，非加载进度；完成动画不是整个操作已成功，仍以 await 为准。Godot 提供 delay／Tween 缓动，Unity Simple 提供 AnimationCurve；两端参数不完全相同，见 API。

## 相关章节

[时间轴](timeline-animation.md) · [Godot API](../api/godot.md) · [Unity API](../api/unity.md)
