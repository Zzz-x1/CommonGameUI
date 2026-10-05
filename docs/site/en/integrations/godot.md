# Godot integration

## Project structure

Use the two ProjectReferences in [Godot Getting Started](../getting-started/godot.md). Resource roots must be Control. The framework owns dynamically instantiated PackedScenes; original scenes own borrowed mounts and embedded tabs. Resource paths use `res://…tscn`.

```csharp
var ui = GodotUI.Create(this); // owner must be inside the tree, on the scene thread.
ui.Register<InventoryView>("res://UI/Inventory.tscn");
var windows = await ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", mount);
var inventory = await windows.OpenAsync<InventoryView>();
Button close = inventory.GetControl<Button>("Content/Close");
```

AttachToAsync and Control BindAsync borrow existing instances without loading. GetControl returns the root; `GetControl<T>`(path) looks up a relative widget path and fails on missing/wrong types. Logical Views are not Godot Node scripts attached to resources.

## Layout, input and animation

Empty mounts and full-screen roots use MouseFilter.Ignore; actual panels and backdrops receive input. Keep button FocusMode enabled. The project controls Disabled; input gates do not overwrite that business state.

Scale/Slide targets must not be directly positioned by a layout Container; use a content wrapper. AnimationPlayer samples the initial key before yielding. If a project performance waits for layout, hide incoming presentation first. Do not conceal layout problems with arbitrary delays.

## Resources and shutdown

GodotSceneLoader acquires PackedScenes in the background and instantiates on the scene thread. Native requests cannot be canceled directly; cancellation awaits their completion. Release returns framework references, not guaranteed immediate GPU memory. Replace services via GodotUI.Create(owner, loader: customLoader, logger: customLogger).

Await ui.DisposeAsync before releasing the scene. Borrowed controls are not individually QueueFree'd. For project-created controls whose ownership is transferred, explicitly use GodotUIElement(control, owned: true). Background calls are not automatically dispatched to the UI thread.

[Godot API](../api/godot.md) · [Lifecycle](../manual/lifecycle.md)
