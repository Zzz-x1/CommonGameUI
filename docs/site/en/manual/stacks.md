# Page history

Lobby→inventory→details→back restores the original inventory. Home returns to the retained lobby instance.

## Usage

Each page has its own resource under one Stack. History retains covered pages; Current is the top page.

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

## Key points

- Push creates a page and hides the current one. Pop disposes the top and restores the preceding page.
- Pop requires an earlier page; PopTo takes an earlier instance in this stack.
- PopTo cleans intermediate pages and restores only the target.

[Lifecycle](lifecycle.md) · [Return rules](input-and-focus.md)
