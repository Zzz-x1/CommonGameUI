# Unity integration

Follow [Unity Getting Started](../getting-started/unity.md) for setup.

## Resources and controls

- Prefab roots use RectTransform under Resources; register paths without extensions.
- Mounts/roots have no raycastable full-screen Graphic. Modal creates the backdrop.
- Tabs bind existing RectTransforms without cloning. GetRect/GetComponent access native controls.

```csharp
var ui = UnityUI.Create(canvas, eventSystem);
ui.Register<InventoryView>("UI/Inventory");
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
pages.ChildTransition = UnityTransitions.SlideHorizontal(targetPath: "Content");
var inventory = await pages.PushAsync<InventoryView>();
Button close = inventory.GetComponent<Button>("Content/Close");
```

## Lifecycle and animation

- Create configures a hidden persistent frame pump. Use BindLifetime for a Scene or GameObject; see [Lifecycle](../manual/lifecycle.md).
- Input gates preserve business interactable state. EventSystem configures keyboard navigation.
- Timelines use non-looping legacy AnimationClips and unscaled time.
- Resources requests cannot be canceled directly; releasing references does not immediately clear engine caches.

## Verification status

Current environment: Unity 6000.3.24f1/uGUI 2.0.0. Lifetime bindings passed 7 isolated batch-mode Play Mode tests. Earlier fixes and tutorials have offline compilation only, with their native execution pending. Demo timeline assets also await verification. The Unity Source Generator workflow is not integrated.

[Unity API](../api/unity.md) · [Timelines](../manual/timeline-animation.md)
