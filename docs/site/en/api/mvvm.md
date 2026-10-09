# MVVM API

The runtime namespace is `UIFramework.Mvvm`. Engine targets, configuration and asset adapters belong to the `.Godot` / `.Unity` namespaces.

Projects do not construct, enable or dispose a Runtime. Core invokes the module during Context creation and cleanup; use `ShareMvvm` for sharing. Runtime and `ConfigureMvvm` signatures below serve adapters, preview and advanced configuration. Ordinary code uses Context and Node APIs.

## Runtime

```csharp
new MvvmRuntime(IUILog? log = null);
void RegisterFactory<T>(Func<T> factory) where T : class;
void RegisterFactory(Type type, Func<object> factory);
T GetSharedViewModel<T>(T template, IViewModelAssets assets) where T : class;
void RegisterGlobalViewModelProvider(IGlobalViewModelProvider provider);
T GetGlobalViewModel<T>(string name) where T : class;
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
T GetGlobalViewModel<T>(this UIContext ui, string name) where T : class;
Task WhenMvvmIdleAsync(this UIContext ui);
MvvmRuntime GetMvvm(this UIContext ui);
void ConfigureMvvm(this UIContext ui, MvvmRuntime? runtime = null,
    Func<UINode, IViewBindingConfiguration?>? configuration = null, IViewModelAssets? assets = null);
void SetViewModel(this IUINode node, string name, object vm);
T GetViewModel<T>(this IUINode node, string name) where T : class;
T CreateViewModel<T>(this IUINode node, string name, object? template = null) where T : class;
IDisposable Bind(this IUINode node, string path, IBindingTarget target,
    BindingMode mode = BindingMode.OneWay);
IDisposable Bind(this IUINode node, string path, IBindingTarget target, BindingOptions options);
void PauseBinding(this IUINode node, string propertyName);
void ResumeBinding(this IUINode node, string propertyName);
IDisposable BindCommand(this IUINode node, string path, ICommandTarget target,
    object? parameter = null);
IDisposable BindCollection(this IUINode node, string path, ICollectionTarget target,
    bool oneTime = false);
void ObserveMvvmTask(this IUINode node, Task task);
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
BindingOptions Converters.Configure(string converter, string convertBack, BindingMode mode,
    ConverterInput input, string constant, bool hasNullValue, object? nullValue);
bool Converters.SupportsConstant(Type type);
void PreviewSources.Register<T>(Func<T> factory) where T : class;
```

Built-in names: `FormatNumber`, `PercentText`, `InvertBoolean`, `IntToFloat`, `FloatToInt`. Project converters use the fully qualified type name plus method name. Reverse conversions must be supplied explicitly.

`Converters.Configure` is shared by GUI and preview. ViewModel input uses the binding source path; Constant input parses the literal as the function's input type. Constants support OneWay / OneTime only. `BindingOptions.HasConstantValue` / `ConstantValue` can also explicitly configure a constant without a VM source.

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
| Collection | `CollectionTarget(owner, mount, scene)` | `CollectionTarget(owner, mount, prefab)` |

Godot `MvvmBindings` stores named sources and bindings; Unity stores only binding rows. Godot uses `ViewModelSourceResource` / `ViewBindingResource`; Unity uses `ViewBindingData` with source strings. Binding kinds are Property, Command and Collection.

Custom integrations implement `IViewModelAssets`, `IBindingTarget`, `ICommandTarget`, `ICollectionTarget`, or use `BindingValueTarget<T>`. Targets handle native control access and events; the module handles subscriptions, paths, conversions, ownership and tasks.

`ViewBindingData` / `ViewBindingResource.GetOptions(bool preview = false)` validates conversion settings; preview only updates displays. Unity sources use `ViewBindingData.Source`, resolving local sources first and then provider names; Godot uses `ViewModelSourceResource.Template`, without type names. Godot `MvvmBindings.AddBinding(Control target, ViewBindingResource binding)` adds the reference and configuration together; `GetTarget(ViewBindingResource binding)` returns the native control. GUI edits maintain matching `Targets` and `Bindings` rows.

Related: [Getting started](../manual/mvvm.md) · [GUI](../manual/mvvm-editor.md)

## Extensible targets and collection creation

```csharp
BindingTargetDefinition BindingTargetDefinition.Create<TTarget, TValue>(
    string member, Func<TTarget, TValue> read, Action<TTarget, TValue> write,
    Func<TTarget, Action, Action>? subscribe = null) where TTarget : class;
IReadOnlyList<BindingTargetDefinition> BindingTargetRegistry.Members(Type targetType);
BindingTargetDefinition BindingTargetRegistry.Find(Type targetType, string member);
IBindingTarget BindingTargetDefinition.CreateTarget(object target,
    BindingMode mode = BindingMode.OneWay);
IDisposable BindCommand(this IUINode node, string path, ICommandTarget target,
    CommandBindingOptions options);
Task<IUINode> ICollectionItemFactory.CreateAsync(CollectionItemContext context,
    CancellationToken cancellationToken);
Task<IUINode> CollectionItemContext.LoadAsync(string resource,
    Action<IUINode>? configure = null, CancellationToken cancellationToken = default);
```

`BindingTargetDefinition` exposes TargetType, Member, ValueType and Observable. Providers implement `GetTargets()` and are declared with the assembly attribute `BindingTargetProvider`. Subscribe returns a disconnect action. `CommandBindingOptions` exposes Mode (None/Constant/SourcePath), ConstantValue, ConstantText and ParameterPath; assigning ParameterPath selects SourcePath. `MvvmMember.ParameterType` is null for commands without a business argument.

Both engine CollectionTarget constructors add optional preview, factory and sources arguments. `CollectionItemContext.CreateAsync` is an engine extension accepting GameObject or PackedScene, the configure callback and token. Factory returns the node created by that context. Built-in targets implement IAsyncDisposable; custom ICollectionTarget implementations may do so for awaited cleanup. `Changed` signals layout changes.

Built-in targets and `CollectionTargetBase` include item creation and removal cleanup in the owning node's initialization wait, including nested collections. Changes after the node is ready remain incremental updates. Implement `ICollectionTarget` for synchronous targets; derive asynchronous targets from `CollectionTargetBase` for preparation, cancellation and release. `ObserveMvvmTask` observes tasks rather than acting as an initialization barrier.

## Global providers

```csharp
interface IGlobalViewModelProvider
{
    IReadOnlyDictionary<string, object> ViewModels { get; }
    IViewModelAssets Assets { get; }
}
[assembly: GlobalViewModelProvider(typeof(ProjectProvider))]
// UIFramework.Mvvm.Unity
class GlobalViewModelCatalog : ScriptableObject
{
    List<ScriptableObject> ViewModels;
}
class GlobalViewModelProvider : IGlobalViewModelProvider
{
    const string ResourcePath = "UIFramework/Mvvm";
    GlobalViewModelProvider(IEnumerable<ScriptableObject> viewModels);
    static GlobalViewModelProvider Load();
}
```

Unity automatically registers its default Provider. Assembly attributes register custom Providers; provide a parameterless constructor and preserve its entry point for IL2CPP. Providers expose definitions and asset adapters, while the Runtime owns Context instances. Duplicate names, missing names or assets, and unwoven VMs are errors. Godot source editing remains unchanged in this iteration.
