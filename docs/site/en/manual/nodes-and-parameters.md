# Nodes, parameters and dependencies

## Scenario

Equipment details need an item ID, while inventory refresh needs the player model. Separate creation parameters from service dependencies.

## Resources and responsibilities

The details resource owns widgets. `UINode<TArgs>`.Parameters stores creation arguments. A registration factory injects services. Use generic parameters only for nodes that actually need them.

## Usage

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

## Behavior and limits

Arguments are assigned before initialization. Wrong types or missing required arguments fail. A Panel single instance accepts creation arguments on first open; reopening an existing instance rejects non-null creation arguments. Update it through business methods. There is no built-in binding, MVVM or DI container; inject existing services with ordinary constructors. A business model outlives View hiding or disposal.

## Related chapters

[Panels](panels.md) · [Generator](source-generator.md)
