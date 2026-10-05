# Unity integration

## Setup and resources

Prepare the netstandard2.1 core DLL, then add the local UPM package; see [Unity Getting Started](../getting-started/unity.md). No published Git UPM URL is promised and the Unity Source Generator workflow is not verified.

UnityPrefabLoader uses Resources.`LoadAsync<GameObject>` and requires a RectTransform root. Register paths relative to Resources without extensions, such as UI/Inventory. Canvas/EventSystem must be active; Create configures UnityFramePump on the Canvas for frame waits.

```csharp
var ui = UnityUI.Create(canvas, eventSystem);
ui.Register<InventoryView>("UI/Inventory");
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
pages.ChildTransition = UnityTransitions.SlideHorizontal(targetPath: "Content");
var inventory = await pages.PushAsync<InventoryView>();
Button close = inventory.GetComponent<Button>("Content/Close");
```

## Scene configuration

Use CanvasScaler/GraphicRaycaster and configure EventSystem mouse/keyboard modules. Empty mounts have no raycastable Graphic. Modal roots contain no full-screen backdrop; Content Images receive input and the framework creates the backdrop. CanvasGroup gates input while business code retains button interactable control.

GetRect/`GetComponent<T>`(path) access actual widgets and fail on invalid paths. Bind embedded tabs to existing RectTransforms. Owned dynamic Prefabs deactivate and detach before Destroy, avoiding delayed destruction affecting sibling order.

## Shutdown and validation limits

Await Context.DisposeAsync before destroying Canvas, EventSystem or the frame pump. Disabling the pump fails pending frame waits; async void OnDestroy is not normal awaited cleanup. Native load requests cannot be canceled directly. Release removes adapter references while Unity owns Resources caching.

Use native legacy AnimationClip, not Animator. Latest backdrop visibility/first-frame fixes have offline compilation only, and new runtime tests have not executed. Full persistence of Demo timeline assets remains unverified. The compilation environment is 6000.3.24f1, with no claim of verified IL2CPP, mobile or other versions.

[Unity API](../api/unity.md) · [Timelines](../manual/timeline-animation.md)
