# MVVM API

命名空间为 `UIFramework.Mvvm`。引擎目标、配置和资产适配位于 `.Godot`／`.Unity` 子命名空间。

普通项目无需创建、启用或释放 Runtime。Core 在 Context 创建和清理时调用模块；需要共享时使用 `ShareMvvm`。下列 Runtime 与 `ConfigureMvvm` 签名用于适配、预览及高级配置，常规业务直接使用 Context／Node API。

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
void FlushBindings();
ValueTask DisposeAsync();
```

工厂创建新实例，构造依赖由项目传入。共享资产按原始资产身份缓存，跨 Context 共用；Runtime 关闭取消、等待任务并释放拥有的 VM。`Manual` VM 不自动销毁。

`ObserveTasks` 由引擎帧观察器调用，完成的失败原样抛出。`WhenIdleAsync` 等待已观察任务及执行期间产生的延迟释放。项目也应等待自己启动的 UI 操作，退出前完成清理。

命令内等待 Runtime 关闭时，先结束绑定，VM 保留到命令返回后释放。外部 `DisposeAsync` 等待完整释放；`IsDisposed` 表示完整清理已经结束。

Unity 和 Godot 自动驱动刷新。自定义宿主用 `MvvmFrameDriver.Attach(runtime)` 登记 Runtime，在 UI 帧刷新点调用一次 `Tick()`。无引擎测试可用 `runtime.FlushBindings()` 同步刷新待处理的属性。延迟转换或显示失败时，帧入口会在执行其余绑定后传播原始异常。

## Context 与 Node

```csharp
void RegisterViewModelFactory<T>(this UIContext ui, Func<T> factory) where T : class;
void ShareMvvm(this UIContext ui, UIContext owner);
T GetGlobalViewModel<T>(this UIContext ui, string name) where T : class;
Task WhenMvvmIdleAsync(this UIContext ui);
MvvmRuntime GetMvvm(this UIContext ui);
void ConfigureMvvm(this UIContext ui, MvvmRuntime? runtime = null,
    Func<UINode, IViewBindingConfiguration?>? configuration = null, IViewModelAssets? assets = null);
void SetViewModel(this IUINode node, string name, object vm);
void NotifyChanged<TVm>(this TVm vm, string propertyName) where TVm : class;
T GetViewModel<T>(this IUINode node, string name) where T : class;
T CreateViewModel<T>(this IUINode node, string name, object? template = null) where T : class;
IDisposable Bind(this IUINode node, string path, IBindingTarget target,
    BindingMode mode = BindingMode.OneWay,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable Bind(this IUINode node, string path, IBindingTarget target, BindingOptions options);
void PauseBinding(this IUINode node, string propertyName);
void ResumeBinding(this IUINode node, string propertyName);
IDisposable BindCommand(this IUINode node, string path, ICommandTarget target,
    object? parameter = null);
IDisposable BindCollection(this IUINode node, string path, ICollectionTarget target,
    bool oneTime = false);
void ObserveMvvmTask(this IUINode node, Task task);
```

`SetViewModel` 借用已有对象，`CreateViewModel` 创建的实例归节点所有。可以调用返回值的 `Dispose()` 提前解绑。隐藏或关闭后进入 `KeepAlive` 缓存时保留绑定，节点销毁时自动解绑。

路径格式为 `Source.Member`，支持经过已织入 VM 的嵌套路径。四种模式为 `OneTime`、`OneWay`、`OneWayToViewModel`、`TwoWay`。`BindingOptions` 另有 `Convert`、`ConvertBack`、`HasNullValue`、`NullValue`。

## 泛型回调与多来源投影

```csharp
IDisposable Bind<T>(this IUINode node, string path, Action<T> apply,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable Bind<T1, T2>(this IUINode node, string path1, string path2, Action<T1, T2> apply,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable Bind<T1, T2, T3>(this IUINode node, string path1, string path2, string path3, Action<T1, T2, T3> apply,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable Bind<T1, T2, TResult>(this IUINode node, string path1, string path2,
    IBindingTarget target, Func<T1, T2, TResult> project,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable Bind<T1, T2, T3, TResult>(this IUINode node, string path1, string path2, string path3,
    IBindingTarget target, Func<T1, T2, T3, TResult> project,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
```

来源须可通知；输入按路径顺序传入，计算结果须可赋给目标。用任一来源的完整路径暂停整条绑定，用同一路径恢复时只刷新一次。释放绑定或销毁节点会解除全部订阅；隐藏或进入关闭缓存时保留。

## 自定义控件双向输入

```csharp
IDisposable BindTwoWay<T>(this IUINode node, string path, Action<T> apply,
    Func<Action<T>, Action> subscribe,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable BindTwoWay<T1, TEdit>(this IUINode node, string path, Action<T1> apply,
    Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable BindTwoWay<T1, T2, TEdit>(this IUINode node, string path1, string path2,
    Action<T1, T2> apply, Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
IDisposable BindTwoWay<T1, T2, T3, TEdit>(this IUINode node, string path1, string path2, string path3,
    Action<T1, T2, T3> apply, Func<Action<TEdit>, Action> subscribe, Action<TEdit> commit,
    BindingUpdateTiming timing = BindingUpdateTiming.EndOfFrame);
```

`apply` 接收当前来源值。`subscribe` 接收输入处理函数，返回解除订阅的动作。单参数类型重载将控件输入写回当前可写属性。提供 `commit` 时，业务决定如何处理一次 `TEdit` 输入，显示来源可以是只读属性。输入同步提交，回显由 `timing` 决定。属性目标也可在 `BindingOptions.UpdateTiming` 配置。`EndOfFrame` 合并刷新，`Immediate` 在当前调用结束前更新。

## 标记与转换

| 标记 | 作用 |
|---|---|
| `[ViewModel]` | 明确指定需织入的类 |
| `[FieldNotify]` | setter 成功且值改变后通知 |
| `[FieldNotify(nameof(Gold))]` | 计算属性依赖，支持传递依赖 |
| `[Command(CanExecute = nameof(CanBuy))]` | 业务方法及可用条件 |
| `[Converter]` | 公共静态单参数转换函数 |

```csharp
IEnumerable<ConverterInfo> Converters.Discover();
Func<object?, object?> Converters.Find(string name);
BindingOptions Converters.Configure(string converter, string convertBack, BindingMode mode,
    ConverterInput input, string constant, bool hasNullValue, object? nullValue);
bool Converters.SupportsConstant(Type type);
void PreviewSources.Register<T>(Func<T> factory) where T : class;
```

内置转换名称：`FormatNumber`、`PercentText`、`InvertBoolean`、`IntToFloat`、`FloatToInt`。项目转换名称为完整类型名加方法名。反向转换必须显式提供。

`Converters.Configure` 是 GUI／预览共用的配置入口：ViewModel 输入使用绑定来源路径，Constant 输入按函数类型解析常量。常量只允许 OneWay／OneTime。`BindingOptions.HasConstantValue`／`ConstantValue` 也可显式配置常量，不要求 VM 来源。

Weaver 为 `IMvvmObject` 生成 `Read`、`Write`、`CanExecute`、`Execute`、`Describe`、`NotifyChanged` 和 `PropertyChanged`。手写该接口的 VM 也需实现 `void NotifyChanged(string propertyName)`。升级后请用配套 Weaver 重新编译包含 VM 的程序集。

## 引擎适配

| 用途 | Godot | Unity |
|---|---|---|
| 启用配置和观察器 | 自动 | 自动 |
| 共享资产 | `ui.GetSharedViewModel(resource)` | `ui.GetGlobalViewModel<PlayerVm>("Player")` |
| 文本 | Label／LineEdit `.Bindable().Text` | Text／TMP／InputField `.Bindable().Text` |
| 滑杆 | Range `.Bindable().Value` | Slider `.Bindable().Value` |
| 开关 | BaseButton `.Bindable().ButtonPressed` | Toggle `.Bindable().IsOn` |
| 命令 | BaseButton `.Bindable().Click` | Button `.Bindable().Click` |
| 列表 | `CollectionTarget(owner, mount, scene)` | `CollectionTarget(owner, mount, prefab)` |

Godot 和 Unity uGUI 的命名空间为 `UIFramework.Mvvm.Godot` 和 `UIFramework.Mvvm.Unity`。`TMP_Text`、`TMP_InputField` 的 `.Bindable()` 来自 `UIFramework.Mvvm.Unity.TMP`，使用它们的 asmdef 需引用 `Unity.TextMeshPro`。已声明的属性返回 `IBindingTarget`，命令返回 `ICommandTarget`。Unity 的 `.Bindable().Text`、`.Value`、`.IsOn` 分别对应序列化键 `text`、`value`、`isOn`；Godot 保留 `Text`、`Value`、`ButtonPressed`。按成员名动态创建属性目标可用两端的 `BindingTargets.Property(control, member)`，可选参数 `mode` 默认为 `BindingMode.OneWay`。

Godot `MvvmBindings` 保存具名来源和绑定配置，Unity 仅保存绑定列表。Godot 使用 `ViewModelSourceResource`／`ViewBindingResource`；Unity 使用保存来源字符串的 `ViewBindingData`。配置种类为 Property、Command、Collection。

自定义适配实现 `IViewModelAssets`、`IBindingTarget`、`ICommandTarget`、`ICollectionTarget`，或使用 `BindingValueTarget<T>`。目标负责原生控件读写和事件；模块负责订阅、路径、转换、所有权及任务。

`ViewBindingData.Rule`（Unity）和 `ViewBindingResource.Rule`（Godot）将配置行表示为 `BindingRule`。适配器通过 `IViewBindingSlots` 提供每个实例的目标控件和条目工厂，`ViewBindingPlans.Apply(UINode, IViewBindingSlots, ...)` 连接各行。预览沿用相同规则，校验命令但不执行。Unity 来源使用字符串 `ViewBindingData.Source`，先查本地注入，再由 Provider 按名称解析；Godot 使用 `ViewModelSourceResource.Template`，不存类型名。Godot `MvvmBindings.AddBinding(Control target, ViewBindingResource binding)` 同步添加引用与配置，`GetTarget(ViewBindingResource binding)` 返回原生控件。`Targets` 与 `Bindings` 按行对应，由 GUI 共同维护。

相关：[入门](../manual/mvvm.md) · [GUI](../manual/mvvm-editor.md)

## 按键应用集合快照

```csharp
void ApplySnapshot<TRow, TData, TKey>(
    this ObservableCollection<TRow> rows, IReadOnlyList<TData> snapshot,
    Func<TRow, TKey> rowKey, Func<TData, TKey> dataKey,
    Func<TData, TRow> create, Action<TRow, TData> update)
    where TRow : class where TKey : notnull;
```

在 UI 线程调用 `UIFramework.Mvvm.CollectionSnapshotExtensions.ApplySnapshot`。集合与快照各自的键须唯一且非空，框架在修改前验证两侧键。`create` 提供相同键的新行，`update` 须保持键稳定，两个回调均不能重入修改该集合。回调或订阅者失败时传播原异常，已发布的更改保留。集合持有者继续管理 VM 的释放。

## 目标扩展与集合创建

以下标记来自 `UIFramework.Mvvm`，加在独立声明类上，填写的成员名也是 GUI 属性键。见[声明与追加示例](../manual/mvvm.md#声明控件绑定)。

| 标记 | 生成结果 |
|---|---|
| `[ControlAdapter(typeof(TControl))]` | 该类型的主适配、`control.Bindable()` 和可补全的属性。已适配基类的成员也可用 |
| `[ExtendControlAdapter(typeof(TControl))]` | 为现有主适配追加扩展方法，导入声明类所在的命名空间后可用 |
| `[BindProperty(member, Read = ..., Write = ..., Changed = ...)]` | 默认使用同名原生属性读写。可指定访问方法和原生变化事件。提供通知的属性支持输入回写 |
| `[BindCommand(member, Factory = nameof(CreateClick))]` | 工厂接受控件并返回 `ICommandTarget`，供代码传给 `BindCommand` |

```csharp
BindingTargetDefinition BindingTargetDefinition.Create<TTarget, TValue>(
    string member, Func<TTarget, TValue> read, Action<TTarget, TValue> write,
    Func<TTarget, Action, Action>? subscribe = null) where TTarget : class;
IReadOnlyList<BindingTargetDefinition> BindingTargetRegistry.Members(Type targetType);
BindingTargetDefinition BindingTargetRegistry.Find(Type targetType, string member);
bool BindingTargetDefinition.Supports(BindingMode mode);
IBindingTarget BindingTargetDefinition.CreateTarget(object target,
    BindingMode mode = BindingMode.OneWay);
IDisposable BindCommand(this IUINode node, string path, ICommandTarget target,
    CommandBindingOptions options);
Task<IUINode> ICollectionItemFactory.CreateAsync(CollectionItemContext context,
    CancellationToken cancellationToken);
Task<IUINode> CollectionItemContext.LoadAsync(string resource,
    Action<IUINode>? configure = null, CancellationToken cancellationToken = default);
```

`BindingTargetDefinition` 提供 TargetType、Member、ValueType、Observable。提供者实现 `GetTargets()` 并使用程序集 `BindingTargetProvider` 标记；Subscribe 返回解绑函数。`CommandBindingOptions` 包含 Mode（None/Constant/SourcePath）、ConstantValue、ConstantText、ParameterPath；设置 ParameterPath 自动选择 SourcePath。无业务参数的命令其 `MvvmMember.ParameterType` 为 null。

两端 CollectionTarget 构造增加可选 preview、factory、sources 参数。`CollectionItemContext.CreateAsync` 为引擎扩展，接收 GameObject／PackedScene、配置回调与取消 Token。工厂须返回该 context 创建的节点。内置目标实现 IAsyncDisposable；自定义 ICollectionTarget 可实现它以等待清理。

商品行异步就绪后，目标将该行插到集合指定的位置。移动只调整这一行，删除保留其余行的相对顺序。需要刷新布局时可订阅 `Changed`，清空非空列表只通知一次。

两端内置目标及 `CollectionTargetBase` 在所属节点初始化期间，将条目创建和移除收尾纳入初始化等待；嵌套集合同样等待。节点就绪后的集合变化只增量更新。自定义同步目标实现 `ICollectionTarget`；自定义异步目标继承 `CollectionTargetBase`，统一参与等待、取消和释放。`ObserveMvvmTask` 仅观察任务，不作为初始化屏障。

## 全局 Provider

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
    List<GlobalViewModel> ViewModels;
}
class GlobalViewModelProvider : IGlobalViewModelProvider
{
    const string ResourcePath = "UIFramework/Mvvm";
    GlobalViewModelProvider(IEnumerable<GlobalViewModel> viewModels, bool preview = false);
    static GlobalViewModelProvider Load(bool preview = false);
}
```

Unity 默认 Provider 由适配器自动注册；自定义 Provider 可用程序集标记自动注册，需要公开无参构造。Weaver 生成直接调用构造函数的工厂，Unity 启动入口会在 IL2CPP 裁剪后保留。Provider 只提供定义及资产适配，不持有 Context VM 实例。重复名称、缺失名称或资产和未织入 VM 直接报错。Godot 的来源编辑器本轮保持原行为。


## Unity 全局 SO VM

```csharp
// UIFramework.Mvvm.Unity
abstract class GlobalViewModel : ScriptableObject
{
    protected virtual void OnInitialize();
    protected virtual ValueTask OnDisposeAsync();
}
class ViewModelAssets : IViewModelAssets, IViewModelAssetLifecycle, IViewModelAssetPolicy
{
    ViewModelAssets(bool preview = false);
}
// UIFramework.Mvvm: optional adapter hooks
interface IViewModelAssetLifecycle
{
    void Initialize(object instance);
    ValueTask DisposeAsync(object instance);
}
interface IViewModelAssetPolicy
{
    void ValidateCreation(Type type, object? template, bool shared, bool registeredTemplate);
    void ValidateInstance(object instance, bool shared);
    void ValidateBorrowed(object instance, bool ownedShared);
}
```

Unity 仅接受 GlobalViewModel 资产。独立 SO 创建被拒绝；共享模板必须已登记，借用实例须由当前 Runtime 持有。首次使用初始化，Context 最终释放等待清理；preview=true 不执行业务回调。Godot 保留原有资源创建方式。
