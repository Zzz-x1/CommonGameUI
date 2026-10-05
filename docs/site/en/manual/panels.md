# Panels and instances

## Scenario

Inventory should retain filters and scroll while hidden. Equipment comparison needs two independent floating windows. Use Panel hiding and instance policy instead of rebuilding inventory.

## Resources and responsibilities

Register inventory and comparison resources separately. WindowInstanceMode applies only to Panel, not Stack or Modal. Keep comparison instance references and enforce the two-card limit in project code.

## Usage

```csharp
ui.Register<InventoryView>("inventory-resource");
ui.Register<CompareView>("compare-resource", windowMode: WindowInstanceMode.Multiple);
var inventory = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(inventory);
var same = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(same);
await windows.ShowAsync(same);
bool exists = windows.TryGet<InventoryView>(out var existing);
var left = await windows.OpenAsync<CompareView>();
var right = await windows.OpenAsync<CompareView>();
await windows.CloseAsync(left);
await windows.CloseAsync(right);
await windows.CloseAsync(inventory);
```

## Behavior and limits

Single is the default. Repeated Open returns the existing instance, restoring it if hidden; an already shown instance does not re-enter. Hide retains, Show restores, Close disposes; the next Open creates anew. TryGet includes committed hidden instances and fails on multiple matches instead of choosing one. Hiding an already hidden node, showing an already shown node, or using another container’s node fails. Count limits, dragging and card positioning are project rules.

## Related chapters

[Resources](resources.md) · [Recipes](../samples/game-ui-recipes.md)
