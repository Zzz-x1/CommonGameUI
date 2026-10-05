# MVVM API

The runtime namespace is `UIFramework.Mvvm`. Engine targets, configuration and asset adapters belong to the `.Godot` / `.Unity` namespaces.

Projects do not construct, enable or dispose a Runtime. Core invokes the module during Context creation and cleanup; use `ShareMvvm` for sharing. Runtime and `ConfigureMvvm` signatures below serve adapters, preview and advanced configuration. Ordinary code uses Context and Node APIs.

## Runtime

```csharp
new MvvmRuntime(IUILog? log = null);
void RegisterFactory<T>(Func<T> factory) where T : class;
void RegisterFactory(Type type, Func<object> factory);
T GetSharedViewModel<T>(T template, IViewModelAssets assets) where T : class;
Task WhenIdleAsync();
void Observe(Task task);
void ObserveTasks();
ValueTask DisposeAsync();
```

Factories create fresh instances with project-provided constructor dependencies. Shared assets are cached by original asset identity across Contexts. Runtime disposal cancels and awaits tasks, then releases owned VMs. `Manual` VMs are never automatically destroyed.

Engine frame observers call `ObserveTasks`, which rethrows completed failures unchanged. `WhenIdleAsync` waits for observed tasks and deferred release created while they run. Projects must also await their UI operations and finish cleanup before exiting.

When a command awaits Runtime disposal, bindings end first and its VM remains alive until the command returns. External `DisposeAsync` awaits complete release; `IsDisposed` indicates that complete phase has ended.

## Context and Node

```csharp
void RegisterViewModelFactory<T>(this UIContext ui, Func<T> factory) where T : class;
void ShareMvvm(this UIContext ui, UIContext owner);
Task WhenMvvmIdleAsync(this UIContext ui);
MvvmRuntime GetMvvm(this UIContext ui);
void ConfigureMvvm(this UIContext ui, MvvmRuntime? runtime = null,
    Func<UINode, IViewBindingConfiguration?>? configuration = null, IViewModelAssets? assets = null);
void SetViewModel(this UINode node, string name, object vm);
T GetViewModel<T>(this UINode node, string name) where T : class;
T CreateViewModel<T>(this UINode node, string name, object? template = null) where T : class;
IDisposable Bind(this UINode node, string path, IBindingTarget target,
    BindingMode mode = BindingMode.OneWay);
IDisposable Bind(this UINode node, string path, IBindingTarget target, BindingOptions options);
IDisposable BindCommand(this UINode node, string path, ICommandTarget target,
    object? parameter = null);
IDisposable BindCollection(this UINode node, string path, ICollectionTarget target,
    bool oneTime = false);
void ObserveMvvmTask(this UINode node, Task task);
```

Enable MVVM before creating business nodes. `SetViewModel` borrows an object; `CreateViewModel` gives ownership to the node. Returned bindings can be detached early and are also cleaned up with the node. Hiding retains them; closing cleans them up.

Paths use `Source.Member` and may traverse woven intermediate VMs. Modes are `OneTime`, `OneWay`, `OneWayToViewModel` and `TwoWay`. `BindingOptions` also provides `Convert`, `ConvertBack`, `HasNullValue` and `NullValue`.

## Attributes and conversions

| Attribute | Purpose |
|---|---|
| `[ViewModel]` | Explicitly selects a class for weaving |
| `[FieldNotify]` | Notifies after a successful setter changes the value |
| `[FieldNotify(nameof(Gold))]` | Computed dependencies, including transitive dependencies |
| `[Command(CanExecute = nameof(CanBuy))]` | Business method and availability condition |
| `[Converter]` | Public static conversion function with one parameter |

```csharp
IEnumerable<ConverterInfo> Converters.Discover();
Func<object?, object?> Converters.Find(string name);
void PreviewSources.Register<T>(Func<T> factory) where T : class;
```

Built-in names: `FormatNumber`, `PercentText`, `InvertBoolean`, `IntToFloat`, `FloatToInt`. Project converters use the fully qualified type name plus method name. Reverse conversions must be supplied explicitly.

The Weaver implements `IMvvmObject`: `Read`, `Write`, `CanExecute`, `Execute`, `Describe` and `PropertyChanged`. `Describe` returns `MvvmMember[]` containing names, types, member kinds, notification and writable flags. Business code usually does not call these generated entry points.

## Engine integration

| Purpose | Godot | Unity |
|---|---|---|
| Enable configuration and observer | Automatic | Automatic |
| Shared asset | `ui.GetSharedViewModel(resource)` | `ui.GetSharedViewModel(asset)` |
| Text | Label / LineEdit `.TextTarget()` | Text / TMP / InputField `.TextTarget()` |
| Slider | Range `.ValueTarget()` | Slider `.ValueTarget()` |
| Toggle | BaseButton `.ToggleTarget()` | Toggle `.ToggleTarget()` |
| Command | BaseButton `.ClickTarget()` | Button `.ClickTarget()` |
| Collection | `GodotCollectionTarget(owner, mount, scene)` | `UnityCollectionTarget(owner, mount, prefab)` |

`MvvmBindings` stores named sources and bindings. Godot uses `ViewModelSourceResource` / `ViewBindingResource`; Unity uses `ViewModelSourceData` / `ViewBindingData`. Binding kinds are Property, Command and Collection.

Custom integrations implement `IViewModelAssets`, `IBindingTarget`, `ICommandTarget`, `ICollectionTarget`, or use `BindingValueTarget<T>`. Targets handle native control access and events; the module handles subscriptions, paths, conversions, ownership and tasks.

Related: [Getting started](../manual/mvvm.md) · [GUI](../manual/mvvm-editor.md)
