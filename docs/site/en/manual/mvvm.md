# Getting started with MVVM

MVVM is optional. Keep currency, settings and inventory data in a VM and update controls through bindings. Existing framework setup remains unchanged when the module is not installed.

## Install

- **Unity**: run `scripts/Prepare-Mvvm.ps1`, then install `src/UIFramework.Mvvm.Unity` as a local UPM package. Reference `UIFramework.Mvvm.Unity` from your asmdef; text targets also require `Unity.TextMeshPro`. Unity's ILPostProcessor automatically weaves project assemblies.
- **Godot**: run `scripts/Prepare-GodotMvvm.ps1 -Project <project directory>` to install the source plugin. Reference `UIFramework.Mvvm.csproj` and the existing Godot adapter, then import `build/UIFramework.Mvvm.targets`. Enable **UIFramework MVVM** in project settings.
- **Plain .NET**: reference `UIFramework.Mvvm.csproj` and import the same targets. The build tool requires the .NET 10 SDK; the game runtime module targets `netstandard2.1`.

Cecil is a build dependency only. Plain VMs need no framework base class. Unity assets derive from `ScriptableObject`; Godot assets derive from `Resource`.

## First binding

```csharp
[ViewModel]
public class PlayerVm
{
    [FieldNotify]
    public int Gold { get; set; }

    [FieldNotify(nameof(Gold))]
    public bool CanBuy => Gold >= 100;

    [Command(CanExecute = nameof(CanBuy))]
    public void Buy() => Gold -= 100;
}
```

Create the Runtime before business nodes:

```csharp
var runtime = new MvvmRuntime();
ui.UseGodotMvvm(runtime); // Unity: ui.UseUnityMvvm(runtime)
```

Provide sources and bindings during node initialization:

```csharp
this.SetViewModel("Player", player);
this.Bind("Player.Gold", goldLabel.TextTarget(), new BindingOptions
{
    Convert = Converters.Find("FormatNumber")
});
this.BindCommand("Player.Buy", buyButton.ClickTarget());
```

Control extensions belong to `UIFramework.Mvvm.Godot` or `UIFramework.Mvvm.Unity`. Plain C# integrations use `ui.UseMvvm(runtime)` and provide target interfaces. GUI configuration and code use the same binding logic.

## Choose ownership

| Mode | Typical use | Released by |
|---|---|---|
| Create Instance | Equipment details and purchase flows | Node closure |
| Manual | Player state already created by gameplay services | Project |
| Shared Asset | Currency or settings shared across windows | Runtime disposal |

```csharp
runtime.RegisterFactory(() => new InventoryVm(inventoryService));
var inventory = this.CreateViewModel<InventoryVm>("Inventory");
var player = runtime.GetSharedViewModel(playerAsset);
this.SetViewModel("Player", player);
```

The last line borrows the reference; the Runtime still owns the shared clone. Original assets provide initial values and remain unchanged. Hiding keeps bindings and state; closing detaches bindings and releases private VMs.

## Two-way and nested paths

```csharp
this.Bind("Settings.Volume", volumeSlider.ValueTarget(), BindingMode.TwoWay);
this.Bind("Player.Selected.Name", nameLabel.TextTarget(), new BindingOptions
{
    HasNullValue = true,
    NullValue = "No selection"
});
```

Two-way bindings initially use the VM value. After control input, the normalized setter value is synchronized back. Configure forward and reverse conversions separately with `Convert` and `ConvertBack`.

Replacing an intermediate VM rebuilds subscriptions. Intermediate reactive properties also require `FieldNotify`. Null paths throw by default. Explicit null presentation neither hides missing members or wrong types nor creates missing objects.

## Lists and commands

Dynamic lists use `ObservableCollection<T>` or `INotifyCollectionChanged`. Bind them with `BindCollection` and `GodotCollectionTarget` / `UnityCollectionTarget`. Add `MvvmBindings` to the item scene or Prefab and declare a **Manual / Item** source. Items can be basic nodes without a business View class.

Moving retains item instances; removal detaches bindings and releases the item. Ordinary arrays support one-time display. Project resources define layout. The module provides no virtualization, sorting or extra pool.

Commands support `void` and `Task`, at most one business parameter, and an optional `CancellationToken` for asynchronous work. `async void` is rejected. `CanExecute` and execution state control buttons; the Runtime continuously observes the same task. Save business changes before presenting rewards. Cancelled confirmations do not commit.

A command can await closure of its own window or an ancestor. Bindings detach during closure; its VM stays alive until the command finishes. External closure cancels and awaits commands. `runtime.WhenIdleAsync()` also waits for deferred VM release. When a command awaits Runtime shutdown, it ends bindings first; full shutdown completes after that command returns.

Await cleanup before exiting:

```csharp
await ui.DisposeAsync();
await runtime.DisposeAsync();
```

Original exceptions are logged and propagated. UI bindings and notifications run on the engine thread. Direct field writes do not notify.

Related: [GUI configuration](mvvm-editor.md) · [MVVM API](../api/mvvm.md) · [Lifecycle](lifecycle.md)
