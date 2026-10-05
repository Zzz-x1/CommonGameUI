# Resources and preloading

## Scenario

Preload lobby and inventory during login preparation. Opening a window acquires its own resource reference, so releasing the preload lease does not break it.

## Resources and responsibilities

IUIResourceLoader acquires an instantiable IUIResource; Instantiate creates IUIElement on the UI thread. Resource ownership and display instances are separate. UIResourceLease creates no Node and runs no lifecycle.

## Usage

```csharp
var prepared = await ui.PreloadAsync<InventoryView>(cancellationToken);
var inventory = await windows.OpenAsync<InventoryView>();
prepared.Dispose();
await windows.CloseAsync(inventory);
await ui.DisposeAsync();
```

## Behavior and limits

One Context coalesces loading by path, with independent leases for waiters and instances. Canceling one waiter does not cancel others. The last release returns the resource. Nodes without a resource path cannot preload. Context disposal cancels and awaits shared loads, instances and remaining resources. Godot loads in the background, but instantiation and initialization stay on the UI thread. Native Godot/Unity requests cannot be canceled directly, so cancellation may await native completion. Releasing references does not promise immediate GPU/cache reclamation. There is no cache budget, pooling or Addressables backend; implement a project loader if needed.

## Related chapters

[Custom engine](../integrations/custom-engine.md) · [Async shutdown](async-and-errors.md)
