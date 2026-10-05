# Source Generator

## Scenario

Many business nodes need resource registration and typed calls. Reduce repeated Register and generic calls while keeping handwritten dependency-injecting factories.

## Resources and responsibilities

Add an Analyzer project reference to the consuming project, not a runtime generator DLL. The Godot Demo uses it; the Unity workflow does not. Examples use public, non-generic, top-level business classes with parameterless constructors.

## Usage

```csharp
namespace Game.UI
{
    [UIResource("res://Inventory.tscn")]
    public sealed class InventoryView : UINode { }
    [UIResource("res://Compare.tscn", WindowMode = WindowInstanceMode.Multiple)]
    public sealed class CompareView : UINode { }
}
// In caller code: using UIFramework.Generated; using Game.UI;
// ui.RegisterGeneratedUI();
// var inventory = await windows.OpenInventoryViewAsync();
```

## Behavior and limits

Use `<ProjectReference Include=".../UIFramework.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`, adjusting the path. Registration lives in UIFramework.Generated; call extensions live in the business node namespace. Same-named nodes can coexist across namespaces. If importing both makes a call ambiguous, qualify Game.UI.GeneratedUICalls or use the core generic API. Generated calls cover Push, Panel Open and Modal Open. `UINode<TArgs>` produces argument calls plus parameterless Panel reopen; `DialogNode<TResult>` produces Show…Async. playAnimation and cancellationToken are forwarded. There is no dynamic Tab/binding generation or custom Analyzer diagnostic. Keep service constructors in handwritten registrations.

## Generated signature examples

```csharp
// UIFramework.Generated.GeneratedUIRegistration:
void RegisterGeneratedUI(this UIContext ui)
// Game.UI.GeneratedUICalls, for the declared InventoryView:
Task<InventoryView> PushInventoryViewAsync(this StackContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this PanelContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this ModalContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
```

## Related chapters

[Node parameters](nodes-and-parameters.md) · [API](../api/core.md)
