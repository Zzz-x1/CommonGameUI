# MVVM 入门

MVVM 是可选模块。它把金币、设置、背包等界面数据放在 VM 中，通过绑定更新控件。未安装时，原有框架接入方式不变。

## 安装

- **Unity**：运行 `scripts/Prepare-Mvvm.ps1`，通过本地 UPM 路径安装 `src/UIFramework.Mvvm.Unity`。项目 asmdef 引用 `UIFramework.Mvvm.Unity`；使用文本目标时同时引用 `Unity.TextMeshPro`。Unity 的 ILPostProcessor 自动织入项目程序集。
- **Godot**：运行 `scripts/Prepare-GodotMvvm.ps1 -Project <项目目录>`，安装源代码插件。项目引用 `UIFramework.Mvvm.csproj` 和现有 Godot 适配器，并导入 `build/UIFramework.Mvvm.targets`。在项目设置启用 **UIFramework MVVM**。
- **普通 .NET**：引用 `UIFramework.Mvvm.csproj`，导入上述 targets。构建工具需要 .NET 10 SDK；游戏运行时模块仍为 `netstandard2.1`。

Cecil 只用于构建。普通 VM 不需要继承框架基类；Unity 资产继承 `ScriptableObject`，Godot 资产继承 `Resource`。

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
this.Bind("Player.Gold", goldLabel.TextTarget(), new BindingOptions
{
    Convert = Converters.Find("FormatNumber")
});
this.BindCommand("Player.Buy", buyButton.ClickTarget());
```

控件扩展位于 `UIFramework.Mvvm.Godot` 或 `UIFramework.Mvvm.Unity`。纯 C# 项目由 Weaver 自动接入，自行提供目标接口。GUI 配置与代码绑定使用同一执行逻辑。

## Unity 全局 VM 目录

创建 `GlobalViewModelCatalog` SO，保存到 `Assets/Resources/UIFramework/Mvvm/` 并添加 SO VM 引用。默认 Provider 使用资产名作为名称（例如 `Player`）；绑定行保存 `Player.Gold`，不保存 VM 引用；无需来源列表、Creation 或手动初始化 Provider。Context 初始化自动注册目录，首次绑定时创建克隆。原资产保持不变，窗口关闭保留全局实例，Context 释放时销毁；Item 与本地注入对象不进入目录。

业务需要直接修改全局状态时，可以取得该 Context 的实例：

```csharp
var player = ui.GetGlobalViewModel<PlayerVm>("Player");
```

## 选择实例方式

| 方式 | 常见场景 | 释放时机 |
|---|---|---|
| Create Instance | 每张装备详情、每个购买流程 | 节点关闭 |
| Manual | 业务服务已经创建的玩家状态 | 项目负责 |
| Shared Asset | 多个窗口共享金币或设置 | 最后一个共享 Context 关闭 |

```csharp
ui.RegisterViewModelFactory(() => new InventoryVm(inventoryService));
var inventory = this.CreateViewModel<InventoryVm>("Inventory");
var player = ui.GetSharedViewModel(playerAsset);
this.SetViewModel("Player", player);
```

最后一行提供的是借用引用；共享克隆仍由 Runtime 持有。原始资产用于初始值，运行时不修改它。隐藏和 KeepAlive 缓存均保留绑定及 VM；销毁解除绑定并释放独立 VM。见[关闭后缓存](resources.md#关闭后缓存)。

多个 Context 需要共享资产克隆时，在创建节点前调用 `second.ShareMvvm(first)`。最后一个 Context 关闭时自动释放共享 Runtime；无需创建、初始化或释放 Runtime。

## 双向与嵌套绑定

```csharp
this.Bind("Settings.Volume", volumeSlider.ValueTarget(), BindingMode.TwoWay);
this.Bind("Player.Selected.Name", nameLabel.TextTarget(), new BindingOptions
{
    HasNullValue = true,
    NullValue = "No selection"
});
```

双向初始以 VM 为准；控件写入 VM 后再次同步，显示 setter 归一化后的值。正向、反向转换分别配置 `Convert`、`ConvertBack`。

中间对象替换后重新订阅。响应路径的中间属性也需要 `FieldNotify`。空路径默认报错；显式空值不会隐藏成员缺失或类型错误，也不会创建缺失对象。

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

这是同步配对 API，不提供动画包装或自动恢复。动画取消／失败时由项目决定恢复时机；节点已关闭则绑定已清理，不能继续恢复。

## 列表和命令

动态列表使用 `ObservableCollection<T>` 或 `INotifyCollectionChanged`。通过 `BindCollection` 绑定到 `CollectionTarget`；条目场景或 Prefab 添加 `MvvmBindings`，`Item` 来源由框架自动提供，无需声明资产。条目可以是基础节点，无需业务 View 类。

移动保留条目实例，删除解除绑定并释放条目。普通数组只能一次性展示。布局由项目资源决定，模块不提供虚拟化、排序或对象池。

命令支持 `void`、`Task`，最多一个业务参数，异步命令可接收 `CancellationToken`。不支持 `async void`。`CanExecute` 和执行状态控制按钮，Runtime 持续观察同一个任务。业务先保存，再展示奖励；取消确认时不提交。

命令可以等待关闭自己的窗口或祖先节点。销毁时解除绑定，VM 保留到命令结束再释放；外部销毁仍取消并等待命令。`ui.WhenMvvmIdleAsync()` 同时等待延迟的 VM 释放。命令中等待 Runtime 关闭时先结束绑定，完整清理在该命令返回后完成。

退出前等待清理：

```csharp
await ui.DisposeAsync();
```

原始错误会记录并传播。UI 绑定和通知在引擎线程执行；直接改字段不会产生通知。

相关：[GUI 配置](mvvm-editor.md) · [MVVM API](../api/mvvm.md) · [生命周期](lifecycle.md)

## 自定义控件属性

实现 `IBindingTargetProvider.GetTargets()`，在程序集中声明 `[assembly: BindingTargetProvider(typeof(ProjectTargets))]`，返回强类型 `BindingTargetDefinition.Create<TControl, TValue>(member, read, write, subscribe)`。显示绑定可省略 `subscribe`；双向及 View→VM 必须提供通知和解绑。运行时、属性选择器和预览共用定义，查询成员不连接控件事件。

## 命令参数与条目工厂

命令可配置常量或来源路径。来源在点击时读取，不自动转换类型。GUI 常量支持文本、布尔、数字和枚举；取消 Token 由框架管理。

条目映射在创建时借用父 VM 实例，不克隆，也不自动跟随父来源替换。

```csharp
node.BindCommand("Shop.Buy", buyButton.ClickTarget(),
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
