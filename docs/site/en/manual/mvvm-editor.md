# MVVM editor

Add `MvvmBindings` to the view root. In Unity open **Window → UIFramework → View Bindings**. In Godot enable the plugin and use the bottom **View Bindings** panel.

## 1. Add Viewmodels

Add named sources such as `Player`, `Settings` and `Inventory`. Select the VM type, creation mode and optional template asset.

- Use **CreateInstance** for independent details.
- Use **Manual** for business-owned objects; call `SetViewModel` during node initialization.
- Use **SharedAsset** when several views reference the same asset.

The type list includes project types marked with `ViewModel`. Pick native `ScriptableObject` / `Resource` assets. Plain C# classes use a type configuration or registered factory.

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

Manual sources require a project sample:

```csharp
PreviewSources.Register(() => new PlayerVm { Gold = 12000 });
```

Without one, the editor explicitly reports a missing preview source. Samples are project-provided data; the module never invents runtime state.

## Save

Unity stores configuration in the component and saves it with the Prefab / scene, including Undo and Prefab Overrides. Godot uses scene Resources and native Undo. Saving and reopening retains the configuration.

Preview does not save the scene. Stop preview before editing configuration so display values remain separate from authored values.

Related: [Setup and ownership](mvvm.md) · [API](../api/mvvm.md)
