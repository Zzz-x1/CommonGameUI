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

## Closed-instance caching

Reopen inventory with its category, scroll position, and subtree intact, or reuse equipment details to display another item. Register `NodeCacheMode.KeepAlive`; container calls stay the same. The default `None` destroys instances on close.

```csharp
ui.Register<InventoryView>("inventory-resource", cacheMode: NodeCacheMode.KeepAlive);
ui.Register<ItemDetailsView>("item-resource", cacheMode: NodeCacheMode.KeepAlive);

var inventory = await pages.PushAsync<InventoryView>();
await pages.PopAsync();
var same = await pages.PushAsync<InventoryView>();

var details = await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopAsync();
var reused = await pages.PushAsync<ItemDetailsView>(new ItemArgs("shield-01"));
await pages.PopAsync();
await pages.PopAsync();

await pages.ClearCacheAsync<ItemDetailsView>();
await pages.ClearCacheAsync();
await ui.ClearCacheAsync();
```

`same` and `inventory`, and `reused` and `details`, respectively refer to the same instances. Details derive from `UINode<ItemArgs>`. Refresh each opening’s content in `OnEnteringAsync`; create control subscriptions and bindings once during initialization.

- Panel Close, Stack Pop/PopTo, and Modal Close/CloseTo share the policy. Each container retains only the last closed instance of each registered type. Instances never move between containers.
- Cached instances leave TryGet, History, and Current, but remain children owned by their original container. `IsCached` is true throughout their subtree; they are invisible and noninteractive. Hidden windows are still open.
- Reopening updates arguments and runs entry without repeating initialization. `LifecycleContext.IsCacheReuse` is true and `IsFirstEnter` is false. Embedded tabs, history, VMs, and bindings retain their state.
- Bindings, collections, and running commands keep working while cached. Close does not cancel Lifetime. Stop display-only tasks or animations in exit/deactivation callbacks. Final disposal releases bindings, tasks, VMs, and resources.
- Each Dialog opening has a separate result session. Closing an unanswered session cancels its wait. Modal backdrops follow normal release rules and are not cached with the window.
- ClearCache does not close current or hidden windows. Parent, Context, or bound engine-object destruction also releases caches. Failed or canceled reuse preparation disposes that instance and propagates the original error.

LRU, capacity budgets, and a general object pool are deferred. This version does not automatically evict caches of other types.
