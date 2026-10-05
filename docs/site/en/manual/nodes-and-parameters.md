# Nodes, parameters and dependencies

Pass an item ID as creation arguments; inject services such as the player model through constructors.

## Usage

Inherit `UINode<TArgs>` and read Parameters. Register service-dependent Views with Register(factory, resource).

```csharp
public sealed record ItemArgs(string ItemId);
public sealed class ItemDetailsView : UINode<ItemArgs>
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        string id = Parameters.ItemId;
        return Task.CompletedTask;
    }
}
// Inside an async method, after registration:
// ui.Register(() => new InventoryView(playerModel), "inventory-resource");
// await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
```

## Key points

- Arguments are assigned before initialization; wrong types or missing required values fail.
- Arguments are for creation. Reopen an existing single instance without them; refresh through a business method.
- Keep models separate so hiding or disposing a View does not lose player data.

[Panels](panels.md) · [Generator](source-generator.md)
