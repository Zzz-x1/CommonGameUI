# Transitions and conditions

## Scenario

Inventory enters with a full presentation from the lobby, slides from the shop, and uses a short reverse animation on return. Choose using real sources and phases rather than extra route tags.

## Resources and responsibilities

Adapters supply recipes. Node Transition overrides its direct container ChildTransition, then Context.DefaultTransition; the initial default is Instant. Container configuration does not propagate through descendant containers. Animate a content wrapper inside the layout.

## Usage

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

## Behavior and limits

Select accepts one synchronous Func and can inspect Phase, Source/Target, Entering, Direction and business preferences. It resolves once before preparation and must not mutate the framework. Exceptions, null and selection cycles fail without fallback. Source exit and target enter may run concurrently. playAnimation=false skips selection/playback but still awaits resources, lifecycle and cleanup. A higher TransitionOrder places an item above its partner temporarily and restores order. Stages are Preparing/Playing/Completed/Canceled/Failed; progress is animation time, not loading. Completed animation does not guarantee the whole operation succeeded; await it. Godot exposes delay/Tween easing, Unity Simple exposes AnimationCurve; their parameters differ, as shown in API.

## Related chapters

[Timelines](timeline-animation.md) · [Godot API](../api/godot.md) · [Unity API](../api/unity.md)
