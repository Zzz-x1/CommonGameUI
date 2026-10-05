# Source Generator

Generate registration and typed calls from resource attributes to reduce repetition.

## Usage

Reference the generator as an Analyzer. Use public top-level non-generic classes with parameterless constructors. Keep service constructors in handwritten factories.

Add this project reference, adjusting the relative path:

```xml
<ProjectReference Include="../../../src/UIFramework.Generators/UIFramework.Generators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

```csharp
using UIFramework;

namespace Game.UI
{
    public sealed class ItemArgs
    {
        public string ItemId { get; }
        public ItemArgs(string itemId) { ItemId = itemId; }
    }
    [UIResource("res://Inventory.tscn")]
    public sealed class InventoryView : UINode { }
    [UIResource("res://Compare.tscn", WindowMode = WindowInstanceMode.Multiple)]
    public sealed class CompareView : UINode<ItemArgs> { }
}
// In caller code: using UIFramework.Generated; using Game.UI;
// ui.RegisterGeneratedUI();
// var inventory = await windows.OpenInventoryViewAsync();
```

## Key points

- Import UIFramework.Generated for registration and the node namespace for typed calls.
- Do not manually register an automatically registered type again.
- Qualify GeneratedUICalls or use generic APIs when same-named nodes make calls ambiguous.
- Calls forward arguments, playAnimation and cancellationToken. The Unity workflow is not integrated.

## Generated signatures

```csharp
// UIFramework.Generated.GeneratedUIRegistration:
void RegisterGeneratedUI(this UIContext ui)
// Game.UI.GeneratedUICalls, for the declared InventoryView:
Task<InventoryView> PushInventoryViewAsync(this StackContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this PanelContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this ModalContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
```

[Node parameters](nodes-and-parameters.md) · [API](../api/core.md)
