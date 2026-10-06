# MVVM editor

Add `MvvmBindings` to the view root. In Unity open **Window → UIFramework → View Bindings**. In Godot enable the plugin and use the bottom **View Bindings** panel.

## 1. Add Viewmodels

Add named sources such as `Player`, `Settings` and `Inventory`. Select a native VM asset and its creation mode.

- Use **CreateInstance** for independent details.
- Use **Manual** for business-owned objects; call `SetViewModel` during node initialization.
- Use **SharedAsset** when several views reference the same asset.

The asset list discovers native `ScriptableObject` / `Resource` VMs marked `ViewModel`, with search and locate actions. Godot VM resources require `[Tool]`. Asset references determine types; type names are not stored. Plain C# VMs use code creation and binding.

## 2. Create View Bindings

Select the control and target property, then drag a member or enter a path such as `Player.Gold`.

| Scenario | Configuration |
|---|---|
| Currency text | OneWay, FormatNumber |
| Volume slider | TwoWay, separate forward/reverse conversions when needed |
| Buy button | Command, `Shop.Buy` |
| Inventory list | Collection, item Prefab / PackedScene |

Converters are filtered by input and output types. Item sources are named `Item`, for example `Item.Title`. Configure layout in resources: VBoxContainer / GridContainer in Godot, or LayoutGroup / project layout code in Unity.

The table supports search, deletion and reordering. Unity disables reordering while searching; clear search to change the full order. Unity controls provide an Inspector entry. Godot provides binding buttons beneath common control properties.

## Configure converters

To display the integer `Player.Gold` as currency text, select **OneWay → Forward converter → FormatNumber**. The input is bound to `Player.Gold`; currency changes update the text.

Selecting a function shows its input and output types. **Converter input** supports two choices:

- **ViewModel**: enter a source member path. Nested members and object replacement are supported.
- **Constant**: enter a literal such as `125`. It is parsed as the function's input type; no VM is required. Text, booleans, numbers and enums are supported. Numbers use a decimal point without thousands separators.

Constants support OneWay / OneTime. For TwoWay, the forward input comes from the VM and the reverse input comes from the control. Select each function separately; the framework does not infer an inverse. Invalid literals, missing functions and conversion failures raise errors directly.

Custom functions are static methods marked `[Converter]`, with one argument and one return value. Cecil generates direct calls. The picker filters compatible types. Preview and runtime execute the same converter configuration.

## 3. Preview initial values

Select **Preview initial values**. Preview uses a separate Runtime and asset clones. It updates displays and lists without executing commands or writing back to original assets. Stopping restores control values.

A Manual source references an asset to declare its type and provide a preview sample. Preview clones the asset. Runtime still requires the business to call `SetViewModel`; it never substitutes the sample. Missing assets prevent preview.

## Save

Unity stores configuration in the component and saves it with the Prefab / scene, including Undo and Prefab Overrides. Godot stores binding Resources and a native control reference array on the scene component; the editor edits both together with native Undo. Targets do not depend on framework-maintained hierarchy paths. Renaming, reparenting and reopening preserve references.

Preview does not save the scene. Stop preview before editing configuration so display values remain separate from authored values.

Related: [Setup and ownership](mvvm.md) · [API](../api/mvvm.md)

## Commands and collections

For commands, choose None, Constant or SourcePath; the editor shows the business parameter type and checks compatibility. Commands without a business parameter have no parameter fields.

For collections, select an item resource and add parent-to-item source mappings. An optional factory references a Unity component or Godot node implementing `ICollectionItemFactory`. Factories also receive `IsPreview`: provide presentation-only preparation there. Preview never executes commands and restores controls when stopped. Item is supplied automatically.

Custom control members registered through `IBindingTargetProvider` appear in the property picker and work in preview without a separate resolver.

Godot factory node scripts need `[Tool]` for editor preview. When changing a command to one without a business parameter, explicitly clear any old parameter configuration using the editor action.
