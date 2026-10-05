# Nodes, parameters and dependencies

Details receive an item ID; inject the name table through the constructor. Keep creation arguments separate from services.

## Define the node

The Godot resource contains Content/ItemName (Label). Parameters are assigned before initialization:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;

public sealed class ItemArgs
{
    public ItemArgs(string itemId) { ItemId = itemId; }
    public string ItemId { get; }
}
public sealed class ItemDetailsView : UINode<ItemArgs>
{
    private readonly IReadOnlyDictionary<string, string> _names;
    public ItemDetailsView(IReadOnlyDictionary<string, string> names) { _names = names; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        this.GetControl<Label>("Content/ItemName").Text = _names[Parameters.ItemId];
        return Task.CompletedTask;
    }
}
```

## Register and open

```csharp
var itemNames = new Dictionary<string, string> { ["sword-01"] = "Iron Sword" };
ui.Register(() => new ItemDetailsView(itemNames), "res://ItemDetails.tscn");
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
```

## Key points

- `UINode<TArgs>` receives creation arguments; factory registration injects shared services.
- Wrong argument types or unknown IDs fail directly.
- Reopen an existing Single window without creation arguments; refresh through a business method.

[Panels](panels.md) · [Generator](source-generator.md)
