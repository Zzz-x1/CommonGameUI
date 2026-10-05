# Nodes, parameters and dependencies

Details receive an item ID. A registration callback supplies the name table. Attach the business script to the detail scene root.

## Define the node

The Godot resource contains Content/ItemName (Label). Parameters are assigned before initialization:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;
using EngineUI = UIFramework.Godot;

public sealed class ItemArgs
{
    public ItemArgs(string itemId) { ItemId = itemId; }
    public string ItemId { get; }
}
public partial class ItemDetailsView : EngineUI.UINode<ItemArgs>
{
    public IReadOnlyDictionary<string, string> Names { get; set; } = null!;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        this.GetControl<Label>("Content/ItemName").Text = Names[Parameters.ItemId];
        return Task.CompletedTask;
    }
}
```

## Register and open

```csharp
var itemNames = new Dictionary<string, string> { ["sword-01"] = "Iron Sword" };
ui.Register<ItemDetailsView>("res://ItemDetails.tscn", view => view.Names = itemNames);
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
```

## Key points

- `UINode<TArgs>` receives creation arguments; the registration callback injects shared services.
- Wrong argument types or unknown IDs fail directly.
- Reopen an existing Single window without creation arguments; refresh through a business method.

[Panels](panels.md) · [Generator](source-generator.md)
