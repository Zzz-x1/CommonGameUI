# Panels and instances

Retain inventory filters and scroll while hidden, or open multiple comparison cards.

## Usage

Panel defaults to Single. For multiple windows, register WindowInstanceMode.Multiple and keep their instance references.

```csharp
ui.Register<InventoryView>("inventory-resource");
ui.Register<CompareView>("compare-resource", windowMode: WindowInstanceMode.Multiple);
var inventory = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(inventory);
var same = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(same);
await windows.ShowAsync(same);
bool exists = windows.TryGet<InventoryView>(out var existing);
var left = await windows.OpenAsync<CompareView>(new ItemArgs("sword-01"));
var right = await windows.OpenAsync<CompareView>(new ItemArgs("shield-01"));
await windows.CloseAsync(left);
await windows.CloseAsync(right);
await windows.CloseAsync(inventory);
```

CompareView inherits `UINode<ItemArgs>`; each card receives its own item ID.

## Key points

- Open reuses an existing Single instance, showing it if hidden.
- Hide/Show retain the instance. Close disposes it; the next Open creates another.
- TryGet includes hidden instances and fails on multiple matches.
- Repeated Hide/Show or a node from another container fails. The project sets count limits.

[Resources](resources.md) · [Recipes](../samples/game-ui-recipes.md)
