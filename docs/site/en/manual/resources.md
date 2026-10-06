# Resources and preloading

Prepare inventory before the lobby. An open window holds its own resource reference independently of the preload lease.

## Usage

Preload acquires resources without creating Nodes. Loader loads, Resource instantiates and Element represents the display.

```csharp
var prepared = await ui.PreloadAsync<InventoryView>(cancellationToken);
var inventory = await windows.OpenAsync<InventoryView>();
prepared.Dispose();
await windows.CloseAsync(inventory);
await ui.DisposeAsync();
```

## Key points

- A Context shares loading by path. Leases/instances hold independent references; the last release returns the resource.
- Canceling one waiter does not affect others. A resource path is required for preload.
- Context disposal awaits load completion and resource cleanup. Non-cancelable native requests may delay cancellation.
- Godot's default loader calls `ResourceLoader.Load` synchronously on the scene thread; `LoadAsync` does not mean background loading. Loading cannot be interrupted; cancellation is checked afterward.
- Releasing framework references does not guarantee immediate texture/cache reclamation.

[Custom engine](../integrations/custom-engine.md) · [Async shutdown](async-and-errors.md)
