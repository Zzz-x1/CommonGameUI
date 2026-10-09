# Getting started with MVVM

MVVM is optional. Keep currency, settings and inventory data in a VM and update controls through bindings. Existing framework setup remains unchanged when the module is not installed.

## Install

- **Unity**: run `scripts/Prepare-Mvvm.ps1`, then install `src/UIFramework.Mvvm.Unity` as a local UPM package. Reference `UIFramework.Mvvm.Unity` from your asmdef; text targets also require `Unity.TextMeshPro`. Unity's ILPostProcessor automatically weaves project assemblies.
- **Godot**: run `scripts/Prepare-GodotMvvm.ps1 -Project <project directory>` to install the source plugin. Reference `UIFramework.Mvvm.csproj` and the existing Godot adapter, then import `build/UIFramework.Mvvm.targets`. Enable **UIFramework MVVM** in project settings.
- **Plain .NET**: reference `UIFramework.Mvvm.csproj` and import the same targets. The build tool requires the .NET 10 SDK; the game runtime module targets `netstandard2.1`.

Cecil is a build dependency only. Plain VMs need no framework base class. Unity global assets derive from `GlobalViewModel`; Godot assets derive from `Resource`.

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

Create your UIContext as usual. MVVM initializes and releases automatically:

```csharp
var ui = UI.Create(this); // Unity: UI.Create(canvas, events)
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

Control extensions belong to `UIFramework.Mvvm.Godot` or `UIFramework.Mvvm.Unity`. The Weaver installs plain C# integration automatically; projects provide target interfaces. GUI configuration and code use the same binding logic.

## Automatic source resolution

`GetViewModel`, code bindings and GUI bindings use the same rule: use the current node's local source first, then resolve the provider name. Global VMs need no prior injection. Collections or business code must inject `Item`. A local instance with the wrong type raises an error instead of selecting a global instance.

```csharp
var shop = this.GetViewModel<ShopVm>("Shop");
```

Resolution only retrieves instances; business preparation remains project code. The Context releases shared instances. A later `SetViewModel` can supply a local instance with the same name and update existing bindings.

## Unity global VM catalogs

Create a `GlobalViewModelCatalog` SO under `Assets/Resources/UIFramework/Mvvm/` and add SO VM references. The default provider uses asset names as keys (for example `Player`). Rows store `Player.Gold` without VM asset references: no per-view source list, Creation setting, or Provider initialization code. Context initialization registers catalogs and first use creates a clone. Originals remain unchanged; global instances survive view closure and are released with the Context. Item and local injected objects are not catalog entries.

Business code can retrieve the Context instance when needed:

```csharp
var player = ui.GetGlobalViewModel<PlayerVm>("Player");
```

## Unity global SO VMs

Unity SO VMs inherit `GlobalViewModel`; no additional `[ViewModel]` marker or initializer registration is needed. Use them for player, shop, or settings state shared within a Context. Use plain C# VMs for independent details, purchase flows, and list items.

```csharp
using System.Threading.Tasks;
using UnityEngine;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;

[CreateAssetMenu(menuName = "Game/ViewModels/Player")]
public sealed class PlayerVm : GlobalViewModel
{
    [SerializeField] private int gold;
    [FieldNotify] public int Gold { get => gold; set => gold = value; }

    protected override void OnInitialize()
    {
        // Prepare shared business state and subscribe to events.
    }

    protected override ValueTask OnDisposeAsync()
    {
        // Unsubscribe and await cleanup if needed.
        return default;
    }
}
```

Add the asset to a global catalog and resolve it by name. First use clones it, prepares initial data, and calls `OnInitialize` before returning or binding it. Closing, hiding, and caching a window do not reinitialize or release the shared VM. Final Context disposal awaits `OnDisposeAsync`, then destroys the SO.

Original assets and editor previews do not run business callbacks. Initialization failures propagate unchanged; partially initialized instances still receive cleanup. Callbacks may subscribe to business events. Do not use `async void` or replace these callbacks with Unity `OnEnable`/`OnDisable`. Cyclic asset references do not guarantee initialized cyclic business dependencies.

Template values follow Unity serialization rules; use SerializeField or SerializeReference for configuration. Nested VM references are remapped in cloned serialized fields, so public read-only properties also see the correct shared instances.

The framework rejects independent SO creation, SO factory results, and direct shared lookup of unregistered templates. `SetViewModel` may only borrow an existing shared SO clone owned by the current Runtime, not original assets, external instances, or another Runtime's instances. The framework owns creation and destruction; there are no public manual initialization or disposal entry points.


## Plain C# and Godot instance ownership

| Mode | Typical use | Released by |
|---|---|---|
| Create Instance | Equipment details and purchase flows | Node closure |
| Manual | Player state already created by gameplay services | Project |
| Shared Asset | Currency or settings shared across windows | Last sharing Context closes |

```csharp
ui.RegisterViewModelFactory(() => new InventoryVm(inventoryService));
var inventory = this.CreateViewModel<InventoryVm>("Inventory");
var player = ui.GetGlobalViewModel<PlayerVm>("Player");
this.SetViewModel("Player", player);
```

The last line borrows the reference; the Runtime still owns the shared clone. Original assets provide initial values and remain unchanged. Hiding and KeepAlive caching retain bindings and VMs; disposal detaches bindings and releases private VMs. See [closed-instance caching](resources.md#closed-instance-caching).

To share asset clones across contexts, call `second.ShareMvvm(first)` before creating nodes. The last Context automatically releases their Runtime. No Runtime construction, initialization or disposal is required.

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

## Pause bindings for animation

After character training saves a new power value, pause its binding while the control displays the count-up animation, then resume:

```csharp
this.PauseBinding(nameof(HeroVm.Power));
// Commit the business value, then animate the control.
// The application decides completion, cancellation and cleanup timing.
this.ResumeBinding(nameof(HeroVm.Power));
```

The VM can keep updating while paused. Bindings neither overwrite animation nor write control changes back to the VM. Resume immediately displays the latest VM value, including conversion and explicit null handling. OneTime also synchronizes once on resume.

All controls bound to the same source property pause together. Other properties, child nodes, commands and lists continue. If the name matches different source paths, specify the full path, such as `"Hero.Power"`. Repeated pause, resume without pause and missing bindings raise errors.

These are synchronous paired APIs without animation wrappers or automatic recovery. The application decides when to resume after cancellation or failure. Closing the node releases its bindings; do not resume afterward.

## Lists and commands

Dynamic lists use `ObservableCollection<T>` or `INotifyCollectionChanged`. Bind them with `BindCollection` and `CollectionTarget`. Add `MvvmBindings` to the item scene or Prefab; the framework supplies the `Item` source automatically, without an asset declaration. Items can be basic nodes without a business View class.

Moving retains item instances; removal detaches bindings and releases the item. Ordinary arrays support one-time display. Project resources define layout. The module provides no virtualization, sorting or extra pool.

Commands support `void` and `Task`, at most one business parameter, and an optional `CancellationToken` for asynchronous work. `async void` is rejected. `CanExecute` and execution state control buttons; the Runtime continuously observes the same task. Save business changes before presenting rewards. Cancelled confirmations do not commit.

A command can await closure of its own window or an ancestor. Bindings detach during disposal; its VM stays alive until the command finishes. External disposal cancels and awaits commands. `ui.WhenMvvmIdleAsync()` also waits for deferred VM release. When a command awaits Runtime shutdown, it ends bindings first; full shutdown completes after that command returns.

Await cleanup before exiting:

```csharp
await ui.DisposeAsync();
```

Original exceptions are logged and propagated. UI bindings and notifications run on the engine thread. Direct field writes do not notify.

Related: [GUI configuration](mvvm-editor.md) · [MVVM API](../api/mvvm.md) · [Lifecycle](lifecycle.md)

## Custom control properties

Implement `IBindingTargetProvider.GetTargets()` and mark the assembly with `[assembly: BindingTargetProvider(typeof(ProjectTargets))]`. Return strongly typed `BindingTargetDefinition.Create<TControl, TValue>(member, read, write, subscribe)` definitions. `subscribe` is optional for display bindings; TwoWay and View→VM require it. Runtime, the property picker and preview use the same definitions. Member discovery does not subscribe to control events.

## Command parameters and item factories

A command binding can use a typed constant or a source path. The source is read when clicked, without implicit conversion. GUI constants support text, boolean, numbers and enums. CancellationToken remains framework-managed.

Item mappings borrow parent VM instances at creation time; they do not clone or follow replacement of the parent source.

```csharp
node.BindCommand("Shop.Buy", buyButton.ClickTarget(),
    new CommandBindingOptions { ParameterPath = "Selection.ProductId" });

var target = new CollectionTarget(node, mount, itemResource,
    sources: new[] {
        new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" }
    });
node.BindCollection("Inventory.Items", target);
```

Implement `ICollectionItemFactory.CreateAsync(context, token)` to select different item resources. Call `context.CreateAsync(prefabOrScene, configure, token)` or `context.LoadAsync(resourcePath, configure, token)`; the configure callback runs after sources exist and before business initialization. Use `node.CreateViewModel` there for item-owned models. `context.Item`, `Index`, `Owner` and `IsPreview` describe creation; Index is the original index, not a live position.

When a collection is bound/populated in `OnInitializeAsync` or applied through GUI configuration, the framework awaits initial item resources, initialization, nested collections and layout callbacks before starting window entry. `BindCollection` remains a synchronous configuration API; users do not need to wait for the Runtime manually.

Changes or rebinding after the window is shown remain asynchronous incremental updates without blocking the window again. Moving preserves instances. Removal, Reset and closure cancel pending creation and wait for acquired resources to be released. Initialization failure prevents display, cleans other items and propagates the original exception. Listen to `target.Changed` for custom layouts. Derive asynchronous targets from `CollectionTargetBase` to participate in the same preparation and cleanup flow.

`ui.WhenMvvmIdleAsync()` awaits the entire Runtime, including commands and deferred releases. Use it for an external synchronization point rather than window initialization.
