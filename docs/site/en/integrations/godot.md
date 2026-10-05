# Godot integration

Follow [Godot Getting Started](../getting-started/godot.md) for setup.

## Resources and controls

- Attach a business script inheriting the adapter UINode (Control) to the PackedScene root; register res:// paths.
- Empty mounts/full-screen roots use MouseFilter.Ignore; actual panels receive input.
- AttachToAsync/BindAsync borrow Controls. GetControl accesses widgets by relative path.

```csharp
var ui = UI.Create(this);
ui.Register<InventoryView>("res://UI/Inventory.tscn");
var windows = await ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", mount);
var inventory = await windows.OpenAsync<InventoryView>();
Button close = inventory.GetControl<Button>("Content/Close");
```

## Key points

- Use `ui.BindLifetime(sceneRoot)` for a scene, or `node.BindLifetime(owner)` for a node; see [Lifecycle](../manual/lifecycle.md).
- Call Create on the scene thread after owner enters the tree. Replace loader/logger as needed.
- Animate independent content wrappers so layout Containers do not overwrite Slide/Scale positions.
- The framework disposes dynamic scenes; original scenes own borrowed controls. Transfer new-control ownership with owned: true.
- Await ui.DisposeAsync before releasing scenes. Background loading does not allow background widget access.

[Godot API](../api/godot.md) · [Resources](../manual/resources.md)
