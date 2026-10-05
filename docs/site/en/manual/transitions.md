# Transitions and conditions

Fade inventory in from the lobby, slide it in from the shop, and use a short return animation.

## Usage

Priority: node Transition → direct container ChildTransition → Context.DefaultTransition. The default is Instant; configuration does not cross descendant containers.

```csharp
var fade = Transitions.Fade(.15);
var slide = Transitions.SlideHorizontal(.20, targetPath: "Content");
ui.DefaultTransition = fade;
pages.ChildTransition = Transitions.Select(context =>
    context.Direction == TransitionDirection.Backward ? fade :
    context.Source is ShopView ? slide : fade);
var inventory = await pages.PushAsync<InventoryView>();
// The ordering/state properties are available on every node:
inventory.TransitionOrder = 10;
inventory.TransitionChanged += state => decoration.Update(state.Progress);
await pages.PopAsync();
```

## Key points

- Select synchronously chooses a recipe from Phase, Source/Target, Direction or settings, without framework mutation.
- playAnimation=false skips animation only; resources, lifecycle and cleanup still run.
- Higher TransitionOrder temporarily draws above its partner, then restores order.
- Progress measures animation time. Await the operation for its final result; see engine APIs for easing.

[Timelines](timeline-animation.md) · [Godot API](../api/godot.md) · [Unity API](../api/unity.md)
