# MVVM 入门

MVVM 是可选模块。它把金币、设置、背包等界面数据放在 VM 中，通过绑定更新控件。未安装时，原有框架接入方式不变。

## 安装

- **Unity**：运行 `scripts/Prepare-Mvvm.ps1`，通过本地 UPM 路径安装 `src/UIFramework.Mvvm.Unity`。项目 asmdef 引用 `UIFramework.Mvvm.Unity`。普通 uGUI 使用 `UIFramework.Mvvm.Unity` 命名空间；绑定 TextMeshPro 控件时，再引用 `Unity.TextMeshPro` 并导入 `UIFramework.Mvvm.Unity.TMP`。Unity 的 ILPostProcessor 自动织入项目程序集。
- **Godot**：运行 `scripts/Prepare-GodotMvvm.ps1 -Project <项目目录>`，安装源代码插件。项目引用 `UIFramework.Mvvm.csproj` 和现有 Godot 适配器，并导入 `build/UIFramework.Mvvm.targets`。在项目设置启用 **UIFramework MVVM**。
- **普通 .NET**：引用 `UIFramework.Mvvm.csproj`，导入上述 targets。构建工具需要 .NET 10 SDK；游戏运行时模块仍为 `netstandard2.1`。

Cecil 只用于构建。普通 VM 不需要继承框架基类；Unity 全局资产继承 `GlobalViewModel`，Godot 资产继承 `Resource`。

## 第一个绑定

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

正常创建 UIContext 即可，MVVM 自动初始化和释放：

```csharp
var ui = UI.Create(this); // Unity: UI.Create(canvas, events)
```

在节点初始化中提供数据源并绑定：

```csharp
this.SetViewModel("Player", player);
this.Bind("Player.Gold", goldLabel.Bindable().Text, new BindingOptions
{
    Convert = Converters.Find("FormatNumber")
});
this.BindCommand("Player.Buy", buyButton.Bindable().Click);
```

导入 `UIFramework.Mvvm.Godot` 或 `UIFramework.Mvvm.Unity`，在控件后输入 `.Bindable().` 即可补全可用成员。标签的 `Text` 用于显示，按钮的 `Click` 用于执行命令。纯 C# 项目自行提供目标接口。

### 通知 UI 重读服务数据

血量由战斗服务持有时，VM 可以直接读取服务。收到服务的血量事件后，在 UI 线程调用 `NotifyChanged`，已绑定的血条和依赖属性都会刷新。

```csharp
[FieldNotify]
public int Health => combat.Health;

[FieldNotify(nameof(Health))]
public bool IsDefeated => Health <= 0;

private void OnHealthChanged() => this.NotifyChanged(nameof(Health));
```

手动通知接受本 VM 的一个 `[FieldNotify]` 属性名，每次调用都会发布。通过已标记的 setter 修改值时，框架会自动比较新旧值并通知。

## 来源自动解析

`GetViewModel`、代码绑定和 GUI 绑定使用同一规则：先查当前节点的本地来源，没有时按名称查询 Provider。无需提前注入全局 VM。`Item` 必须由集合或业务注入。已有本地实例类型错误时直接报错，不改用全局实例。

背包列表绑定 `Inventory.Items`，详情面板绑定 `Selection`。调用 `SetViewModel("Selection", next)` 只刷新详情，现有列表行和正在加载的行都保留。替换嵌套 VM 时，绑定改为监听新对象；如果它仍提供同一个集合，列表行继续复用。更换集合实例或收到集合的 `Reset` 通知时才重建整表。

```csharp
var shop = this.GetViewModel<ShopVm>("Shop");
```

自动解析只取得实例，业务准备仍由项目执行。共享实例由 Context 释放；后续 `SetViewModel` 可提供同名本地实例，已有绑定随之更新。

## Unity 全局 VM 目录

创建 `GlobalViewModelCatalog` SO，保存到 `Assets/Resources/UIFramework/Mvvm/` 并添加 SO VM 引用。默认 Provider 使用资产名作为名称（例如 `Player`）；绑定行保存 `Player.Gold`，不保存 VM 引用；无需来源列表、Creation 或手动初始化 Provider。Context 初始化自动注册目录，首次绑定时创建克隆。原资产保持不变，窗口关闭保留全局实例，Context 释放时销毁；Item 与本地注入对象不进入目录。

业务需要直接修改全局状态时，可以取得该 Context 的实例：

```csharp
var player = ui.GetGlobalViewModel<PlayerVm>("Player");
```

## Unity 全局 SO VM

Unity SO VM 继承 `GlobalViewModel`，无需额外 `[ViewModel]` 标记或初始化注册。它用于同一 Context 共享的玩家、商店或设置状态；独立详情、购买流程和列表条目使用普通 C# VM。

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

将资产加入全局目录，通过名称获取。首次使用时克隆、准备数据并执行 `OnInitialize`，成功后才返回或绑定。窗口关闭、隐藏和缓存不会重复初始化或释放共享 VM；Context 最终释放时等待 `OnDisposeAsync`，再销毁 SO。

原始资产和编辑器预览不执行业务回调。初始化失败传播原异常，部分初始化实例仍执行清理。回调可订阅业务事件；不要使用 `async void`，也不要用 Unity `OnEnable／OnDisable` 替代框架回调。循环资产引用不保证循环业务依赖已经初始化。

模板初始值遵循 Unity 序列化规则，使用 SerializeField 或 SerializeReference 保存配置。嵌套 VM 引用会在克隆后的序列化字段中重映射，公开只读属性也可以读取正确的共享实例。

框架拒绝独立 SO 创建、SO 工厂返回值和未登记模板的直接共享获取。`SetViewModel` 仅可借用当前 Runtime 已有的共享 SO 克隆，不能传原资产、外部实例或其他 Runtime 的实例。创建和销毁由框架管理，没有公开的手工初始化／销毁入口。


## 普通 C# VM 与 Godot 的实例方式

| 方式 | 常见场景 | 释放时机 |
|---|---|---|
| Create Instance | 每张装备详情、每个购买流程 | 节点销毁 |
| Manual | 业务服务已经创建的玩家状态 | 项目负责 |
| Shared Asset | 多个窗口共享金币或设置 | 最后一个共享 Context 关闭 |

```csharp
ui.RegisterViewModelFactory(() => new InventoryVm(inventoryService));
var inventory = this.CreateViewModel<InventoryVm>("Inventory");
var player = ui.GetGlobalViewModel<PlayerVm>("Player");
this.SetViewModel("Player", player);
```

最后一行提供的是借用引用；共享克隆仍由 Runtime 持有。原始资产用于初始值，运行时不修改它。隐藏和 KeepAlive 缓存均保留绑定及 VM；销毁解除绑定并释放独立 VM。见[关闭后缓存](resources.md#关闭后缓存)。

多个 Context 需要共享资产克隆时，在创建节点前调用 `second.ShareMvvm(first)`。最后一个 Context 关闭时自动释放共享 Runtime；无需创建、初始化或释放 Runtime。

## 双向与嵌套绑定

```csharp
this.Bind("Settings.Volume", volumeSlider.Bindable().Value, BindingMode.TwoWay);
this.Bind("Player.Selected.Name", nameLabel.Bindable().Text, new BindingOptions
{
    HasNullValue = true,
    NullValue = "No selection"
});
```

双向绑定首次显示 VM 的值。玩家输入立即写入 VM。正向和反向转换分别配置 `Convert`、`ConvertBack`。

### 选择控件的刷新时机

首次显示和 `ResumeBinding` 同步执行。之后的属性变化默认使用 `EndOfFrame`。到了 UI 帧刷新点，每条待刷新的绑定只读取一次各来源的最新值，更新一次控件。手动通知、setter 校正后的回显也遵循这个时机。

后续代码需要在当前调用内使用新的控件状态时，显式选择 `Immediate`，例如余额变化后立即禁用订单确认。购买命令在执行时依然读取最新的 VM。

```csharp
this.Bind<bool>("Order.CanConfirm", SetConfirmEnabled,
    timing: BindingUpdateTiming.Immediate);
```

中间对象替换后重新订阅。响应路径的中间属性也需要 `FieldNotify`。空路径默认报错；显式空值不会隐藏成员缺失或类型错误，也不会创建缺失对象。

## 使用多个 VM 计算报价

购物车数量、单价或玩家余额在同一帧变化，下面的报价只计算一次。按 lambda 参数顺序列出来源路径。余额足够时显示总价，余额不足时显示还差多少金币。

```csharp
this.Bind("Player.Gold", "Offer.Price", "Cart.Quantity", quoteLabel.Bindable().Text,
    (int gold, int price, int quantity) =>
    {
        long total = (long)price * quantity;
        return gold >= total ? $"{total}" : $"-{total - gold}";
    });

this.Bind<int>("Cart.Quantity", quantityBadge.ShowCount);
```

传目标和返回值函数可以更新一个控件属性。传同步回调则由项目自由更新表现，`ShowCount` 是本例中角标的显示方法。首次绑定会立即显示；路径中的 VM 被替换后，绑定从当前实例读取全部来源。

## 接入自定义控件的输入

**数量选择器**只编辑一个属性。将控件的显示方法传给 `apply`，在 `subscribe` 中连接输入事件并返回解绑动作。这里的控件方法和事件由项目提供。

```csharp
this.BindTwoWay<int>("Cart.Quantity",
    apply: quantityPicker.SetValueWithoutNotify,
    subscribe: edited =>
    {
        quantityPicker.ValueEdited += edited;
        return () => quantityPicker.ValueEdited -= edited;
    }, timing: BindingUpdateTiming.Immediate);
```

打开窗口时，选择器立即显示 `Cart.Quantity`。玩家输入 120，如果 setter 将数量限制为 99，选择器最终显示 99。框架只订阅一次输入，初始化和回显时控件同步发出的事件不会写回 VM。

**价格范围滑杆**同时显示两个属性。拖动时控件本地预览，松手时发出一个项目定义的 `PriceRange`。由 `commit` 一次提交最低价和最高价。回调中按名称获取当前 VM，替换筛选条件后，后续编辑会提交给新实例。

```csharp
this.BindTwoWay<int, int, PriceRange>("Filter.MinPrice", "Filter.MaxPrice",
    apply: rangeSlider.SetRangeWithoutNotify,
    subscribe: committed =>
    {
        rangeSlider.RangeCommitted += committed;
        return () => rangeSlider.RangeCommitted -= committed;
    },
    commit: range => this.GetViewModel<FilterVm>("Filter")
                         .SetPriceRange(range.Min, range.Max));
```

默认情况下，一帧内多次提交只回显一次最终区间。其他绑定按各自的时机刷新。提交中途失败会立即传播原始异常，已生效的值按所选时机回显。编辑器配置见[声明控件绑定](#声明控件绑定)。

## 暂停绑定，让动画接管

角色培养保存了新的战力，但界面需要逐步增长显示时，先暂停该属性绑定，动画结束后恢复：

```csharp
this.PauseBinding(nameof(HeroVm.Power));
// Commit the business value, then animate the control.
// The application decides completion, cancellation and cleanup timing.
this.ResumeBinding(nameof(HeroVm.Power));
```

暂停期间 VM 可以继续更新；绑定既不覆盖动画，也不将控件变化回写 VM。恢复立即显示 VM 最新值，包含转换器与显式空值规则。OneTime 也在恢复时同步一次。

同一来源属性绑定到多个控件时一起暂停；不影响其他属性、子节点、命令或列表。同名属性来自不同路径时使用完整路径，例如 `"Hero.Power"`。重复暂停、未暂停就恢复、缺失绑定直接报错。

多来源绑定可用任一参与的路径暂停整条绑定。暂停期间忽略输入，恢复时读取各来源的最新值并重绘一次。动画结束、取消或失败时，由项目决定何时恢复。隐藏和关闭后进入缓存的节点保留绑定。释放绑定或销毁节点时会解除输入订阅。

## 列表和命令

动态列表使用 `ObservableCollection<T>` 或 `INotifyCollectionChanged`。通过 `BindCollection` 绑定到 `CollectionTarget`；条目场景或 Prefab 添加 `MvvmBindings`，`Item` 来源由框架自动提供，无需声明资产。条目可以是基础节点，无需业务 View 类。

移动保留条目实例，删除解除绑定并释放条目。普通数组只能一次性展示。布局由项目资源决定，模块不提供虚拟化、排序或对象池。

背包排序或收到服务器整表时，在 UI 线程对已绑定的集合调用 `ApplySnapshot`。用装备实例 ID 区分同型号物品。

```csharp
Equipment.ApplySnapshot(sortedItems,
    row => row.InstanceId, item => item.InstanceId,
    item => new EquipmentRowVm(item), (row, item) => row.UpdateFrom(item));
```

`create` 为新键创建并填充 VM。`update` 刷新保留行，其控件和进行中的加载沿用原实例。缺失的键触发移除。若同一 ID 可能改用另一种模板，将 `(InstanceId, LayoutKind)` 作为键。

命令支持 `void`、`Task`，最多一个业务参数，异步命令可接收 `CancellationToken`。不支持 `async void`。`CanExecute` 和执行状态控制按钮，Runtime 持续观察同一个任务。业务先保存，再展示奖励；取消确认时不提交。

命令可以等待关闭自己的窗口或祖先节点。销毁时解除绑定，VM 保留到命令结束再释放；外部销毁仍取消并等待命令。`ui.WhenMvvmIdleAsync()` 同时等待延迟的 VM 释放。命令中等待 Runtime 关闭时先结束绑定，完整清理在该命令返回后完成。

退出前等待清理：

```csharp
await ui.DisposeAsync();
```

节点销毁或 `WhenMvvmIdleAsync()` 等待多个任务时，即使一个失败，也会等其余任务结束。调用方收到原始异常，通过 `UIFailure.GetSecondaryExceptions(error)` 可取得其他失败和取消原因。同一异常只列一次；同批次的失败优先于取消。

命令和绑定会记录原始错误。UI 绑定和通知在引擎线程执行。直接修改字段不会产生通知。

相关：[GUI 配置](mvvm-editor.md) · [MVVM API](../api/mvvm.md) · [生命周期](lifecycle.md)

## 声明控件绑定

### 适配新的引擎控件

任务列表使用 Unity `Scrollbar` 记录滚动位置时，可在项目中为它声明主适配。将下面的类放入项目的 C# 程序集。

```csharp
using UIFramework.Mvvm;
using UnityEngine.UI;

namespace Game.UI.Bindings
{
    [ControlAdapter(typeof(Scrollbar))]
    [BindProperty(nameof(Scrollbar.value),
        Write = nameof(Scrollbar.SetValueWithoutNotify),
        Changed = nameof(Scrollbar.onValueChanged))]
    internal static class QuestScrollbarAdapter { }
}
```

在界面脚本导入 `Game.UI.Bindings`，调用 `this.Bind("Quest.ScrollPosition", scrollbar.Bindable().Value, BindingMode.TwoWay)`。读取使用原生 `value` 属性，VM 回显调用无通知 setter，控件事件通知绑定重读当前值。纯显示属性可以省略 `Changed`。在 GUI 中选择注册名 `value` 即可使用同一属性。

### 给已有适配追加成员

Unity 的 `Slider` 已有主适配。设置页需要绑定允许的最低音量时，在项目中追加 `minValue`。

```csharp
using UIFramework.Mvvm;
using UnityEngine.UI;

namespace Game.UI.Bindings
{
    [ExtendControlAdapter(typeof(Slider))]
    [BindProperty(nameof(Slider.minValue))]
    internal static class SettingsSliderAdapter { }
}
```

在界面脚本导入 `UIFramework.Mvvm.Unity` 和 `Game.UI.Bindings` 后即可绑定：

```csharp
this.Bind("Settings.MinVolume", slider.Bindable().MinValue());
this.Bind("Settings.Volume", slider.Bindable().Value, BindingMode.TwoWay);
```

主适配暴露**属性**，例如 `Value`；追加声明暴露**扩展方法**，例如 `MinValue()`。在 View Bindings 里选择同一个滑杆，成员为 `minValue` 或 `value`。每种原生控件只能声明一个主适配。代码补全取决于变量的声明类型：声明为 `Slider` 可看到 `Value`；声明为 `Selectable` 时只显示该类型和已适配基类的成员。

Godot 的声明方式相同。内置 `Range.Value` 绑定以 `float` 读写，回显调用 `SetValueNoSignal`。Godot Demo 为 `Godot.Range` 追加 `[BindProperty(nameof(Godot.Range.MinValue))]`，代码入口为 `slider.Bindable().MinValue()`，GUI 成员名为 `MinValue`。

### 构建与注册

Roslyn 生成可补全的 C# 入口及强类型属性 Provider，Cecil 织入 VM 访问和 Provider 的注册入口。两者都是构建工具。Unity 的 MVVM UPM 包内含标记为 `RoslynAnalyzer` 的生成器。Godot／.NET 的入口项目和每个声明适配或 Provider 的类库都要导入 `build/UIFramework.Mvvm.targets`；类库作为随项目复制的引用交付，启动和预览时会自动注册。

运行时才能确定成员名时，使用引擎适配层的 `BindingTargets.Property(control, member)`。手工实现的 `IBindingTargetProvider` 也可向同一注册表提供定义。代码绑定、属性选择器和预览复用定义，每个控件实例独立持有订阅。

## 命令参数与条目工厂

命令可配置常量或来源路径。来源在点击时读取，不自动转换类型。GUI 常量支持文本、布尔、数字和枚举；取消 Token 由框架管理。

条目映射在创建时借用父 VM 实例，不克隆，也不自动跟随父来源替换。

```csharp
node.BindCommand("Shop.Buy", buyButton.Bindable().Click,
    new CommandBindingOptions { ParameterPath = "Selection.ProductId" });

var target = new CollectionTarget(node, mount, itemResource,
    sources: new[] {
        new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" }
    });
node.BindCollection("Inventory.Items", target);
```

实现 `ICollectionItemFactory.CreateAsync(context, token)` 可按条目选择不同资源。调用 `context.CreateAsync(prefabOrScene, configure, token)` 或 `context.LoadAsync(resourcePath, configure, token)`；配置回调在来源建立后、业务初始化前执行，可调用 `node.CreateViewModel` 创建条目拥有的模型。`context.Item`、`Index`、`Owner`、`IsPreview` 描述创建请求；Index 是创建时索引，不是实时位置。

在 `OnInitializeAsync` 中绑定／填充集合，或通过 GUI 配置初始集合时，框架自动等待初始条目的资源、初始化、嵌套集合及布局回调完成，再进入窗口入场流程。`BindCollection` 仍是同步配置 API，用户无需手动等待 Runtime。

窗口显示后的增删、替换或重新绑定继续异步增量更新，不重新阻塞窗口。移动保留实例；删除、Reset 和销毁取消待创建条目，并等待已取得资源释放。初始化失败阻止窗口显示，清理其他条目后传播原异常。自定义布局监听 `target.Changed`；异步目标继承 `CollectionTargetBase`，即可参与相同的等待与释放流程。

`ui.WhenMvvmIdleAsync()` 等待整个 Runtime 的任务，包括命令和延迟释放，适用于流程外的统一等待；不要用它代替窗口初始化等待。
