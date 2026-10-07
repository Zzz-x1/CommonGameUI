# PanelContainerNode: independent windows and instances

Use it to open, hide or close independent windows: retain inventory filters and scroll while hidden, or show two equipment comparison cards together. Opening one window does not hide other windows or create return history.

## Resource structure

Each window uses a separate Prefab/PackedScene. Mount the Panel on the project's window area to manage its instances:

```text
WindowMount              ← PanelContainerNode mount
├─ InventoryView         ← single instance, visible or hidden
├─ CompareView(item A)   ← multiple instances
└─ CompareView(item B)   ← multiple instances
```

## Usage

Follow [Create and mount](concepts.md#create-and-mount) to create a `PanelContainerNode`, named windows below. Single is the default registration mode; reopening reuses an existing instance.

```csharp
ui.Register<InventoryView>("inventory-resource");
var inventory = await windows.OpenAsync<InventoryView>();
await windows.HideAsync(inventory);
var same = await windows.OpenAsync<InventoryView>();
bool exists = windows.TryGet<InventoryView>(out var existing);
await windows.CloseAsync(inventory);
```

same, existing and inventory are the same instance. Hide retains node/control state; Open or Show restores visibility. By default Close disposes it and the next Open creates another; KeepAlive enables [closed-instance caching](resources.md#closed-instance-caching). Replace resource strings with Godot res:// paths or Unity Resources paths.

## Open multiple instances together

```csharp
ui.Register<CompareView>("compare-resource", windowMode: WindowInstanceMode.Multiple);
var left = await windows.OpenAsync<CompareView>(new ItemArgs("sword-01"));
var right = await windows.OpenAsync<CompareView>(new ItemArgs("shield-01"));
await windows.CloseAsync(left);
await windows.CloseAsync(right);
```

CompareView inherits `UINode<ItemArgs>`; each card receives its own item ID.

## Behavior and boundaries

- Open reuses an existing Single instance, showing it if hidden.
- Hide/Show retain the instance. Close destroys by default; KeepAlive removes the window from the open list and caches it.
- TryGet includes hidden instances and fails on multiple matches.
- Repeated Hide/Show or a node from another container fails. The project sets count limits.
- Use [Stack](stacks.md) for automatic hiding and stepwise menu history, and [Modal](modals-and-dialogs.md) for confirmation that blocks background input. Panel does not automatically restore input or focus to a previous window.

[Container selection](concepts.md#choose-by-the-flow) · [Resources](resources.md) · [Recipes](../samples/game-ui-recipes.md)
