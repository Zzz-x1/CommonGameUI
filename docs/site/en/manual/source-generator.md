# Source Generator

Generate registration and typed calls from resource attributes to reduce repetition.

## Usage

Reference the generator as an Analyzer. Use public top-level native node classes attached to their declared scene root. Godot scripts require partial. Use handwritten resource registration with a configuration callback to supply services.

Add this project reference, adjusting the relative path:

```xml
<ProjectReference Include="../../../src/UIFramework.Generators/UIFramework.Generators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

```csharp
using UIFramework;
using EngineUI = UIFramework.Godot;

namespace Game.UI
{
    public sealed class ItemArgs
    {
        public string ItemId { get; }
        public ItemArgs(string itemId) { ItemId = itemId; }
    }
    [UIResource("res://Inventory.tscn", CacheMode = NodeCacheMode.KeepAlive)]
    public partial class InventoryView : EngineUI.UINode { }
    [UIResource("res://Compare.tscn", WindowMode = WindowInstanceMode.Multiple)]
    public partial class CompareView : EngineUI.UINode<ItemArgs> { }
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
