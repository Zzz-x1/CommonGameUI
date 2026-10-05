# Page history

## Scenario

Lobby→inventory→item details→back restores the same inventory instance and filters. “Home” returns directly to the earlier lobby instance.

## Resources and responsibilities

Pages have separate resources under one Stack. History retains covered nodes hidden but alive. Keep the lobby instance as the Home target.

## Usage

```csharp
var lobby = await pages.PushAsync<LobbyView>();
var inventory = await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopAsync();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("shield-01"));
await pages.PopToAsync(lobby);
var current = pages.Current;
var history = pages.History;
```

## Behavior and limits

Push creates a node and covers the current page. Pop disposes it and restores the preceding page; an earlier item is required. PopTo accepts a concrete earlier instance in this stack, not Current or an external object. Intermediate pages are cleaned after commit without sequential reactivation. The top exits, the target prepares and enters for return; intermediate hidden pages do not replay display lifecycle. There is no Replace API; express navigation policy through the actual game flow.

## Related chapters

[Lifecycle](lifecycle.md) · [Return rules](input-and-focus.md)
