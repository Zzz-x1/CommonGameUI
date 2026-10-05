# MVVM API

命名空间为 `UIFramework.Mvvm`。引擎目标、配置和资产适配位于 `.Godot`／`.Unity` 子命名空间。

普通项目无需创建、启用或释放 Runtime。Core 在 Context 创建和清理时调用模块；需要共享时使用 `ShareMvvm`。下列 Runtime 与 `ConfigureMvvm` 签名用于适配、预览及高级配置，常规业务直接使用 Context／Node API。

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

工厂创建新实例，构造依赖由项目传入。共享资产按原始资产身份缓存，跨 Context 共用；Runtime 关闭取消、等待任务并释放拥有的 VM。`Manual` VM 不自动销毁。

`ObserveTasks` 由引擎帧观察器调用，完成的失败原样抛出。`WhenIdleAsync` 等待已观察任务及执行期间产生的延迟释放。项目也应等待自己启动的 UI 操作，退出前完成清理。

命令内等待 Runtime 关闭时，先结束绑定，VM 保留到命令返回后释放。外部 `DisposeAsync` 等待完整释放；`IsDisposed` 表示完整清理已经结束。

## Context 与 Node

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

在创建业务节点前启用。`SetViewModel` 接受借用对象；`CreateViewModel` 的实例归节点所有。返回的绑定可提前解除，也会随节点清理。隐藏保留，关闭清理。

路径格式为 `Source.Member`，支持经过已织入 VM 的嵌套路径。四种模式为 `OneTime`、`OneWay`、`OneWayToViewModel`、`TwoWay`。`BindingOptions` 另有 `Convert`、`ConvertBack`、`HasNullValue`、`NullValue`。

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
void PreviewSources.Register<T>(Func<T> factory) where T : class;
```

内置转换名称：`FormatNumber`、`PercentText`、`InvertBoolean`、`IntToFloat`、`FloatToInt`。项目转换名称为完整类型名加方法名。反向转换必须显式提供。

Weaver 实现 `IMvvmObject`：`Read`、`Write`、`CanExecute`、`Execute`、`Describe` 和 `PropertyChanged`。`Describe` 返回 `MvvmMember[]`，包含名称、类型、成员种类、通知及可写标记；业务代码通常不直接使用这些入口。

## 引擎适配

| 用途 | Godot | Unity |
|---|---|---|
| 启用配置和观察器 | 自动 | 自动 |
| 共享资产 | `ui.GetSharedViewModel(resource)` | `ui.GetSharedViewModel(asset)` |
| 文本 | Label／LineEdit `.TextTarget()` | Text／TMP／InputField `.TextTarget()` |
| 滑杆 | Range `.ValueTarget()` | Slider `.ValueTarget()` |
| 开关 | BaseButton `.ToggleTarget()` | Toggle `.ToggleTarget()` |
| 命令 | BaseButton `.ClickTarget()` | Button `.ClickTarget()` |
| 列表 | `GodotCollectionTarget(owner, mount, scene)` | `UnityCollectionTarget(owner, mount, prefab)` |

`MvvmBindings` 保存具名来源和绑定配置。Godot 使用 `ViewModelSourceResource`／`ViewBindingResource`；Unity 使用 `ViewModelSourceData`／`ViewBindingData`。配置种类为 Property、Command、Collection。

自定义适配实现 `IViewModelAssets`、`IBindingTarget`、`ICommandTarget`、`ICollectionTarget`，或使用 `BindingValueTarget<T>`。目标负责原生控件读写和事件；模块负责订阅、路径、转换、所有权及任务。

相关：[入门](../manual/mvvm.md) · [GUI](../manual/mvvm-editor.md)
