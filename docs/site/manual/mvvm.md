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

先创建 Runtime，再创建业务节点：

```csharp
var runtime = new MvvmRuntime();
ui.UseGodotMvvm(runtime); // Unity: ui.UseUnityMvvm(runtime)
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

控件扩展位于 `UIFramework.Mvvm.Godot` 或 `UIFramework.Mvvm.Unity`。纯 C# 接入使用 `ui.UseMvvm(runtime)`，自行提供目标接口。GUI 配置与代码绑定使用同一执行逻辑。

## 选择实例方式

| 方式 | 常见场景 | 释放时机 |
|---|---|---|
| Create Instance | 每张装备详情、每个购买流程 | 节点关闭 |
| Manual | 业务服务已经创建的玩家状态 | 项目负责 |
| Shared Asset | 多个窗口共享金币或设置 | Runtime 关闭 |

```csharp
runtime.RegisterFactory(() => new InventoryVm(inventoryService));
var inventory = this.CreateViewModel<InventoryVm>("Inventory");
var player = runtime.GetSharedViewModel(playerAsset);
this.SetViewModel("Player", player);
```

最后一行提供的是借用引用；共享克隆仍由 Runtime 持有。原始资产用于初始值，运行时不修改它。隐藏节点保留绑定及 VM，关闭解除绑定并释放独立 VM。

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

## 列表和命令

动态列表使用 `ObservableCollection<T>` 或 `INotifyCollectionChanged`。通过 `BindCollection` 绑定到 `GodotCollectionTarget`／`UnityCollectionTarget`；条目场景或 Prefab 添加 `MvvmBindings`，声明 **Manual / Item** 来源。条目可以是基础节点，无需业务 View 类。

移动保留条目实例，删除解除绑定并释放条目。普通数组只能一次性展示。布局由项目资源决定，模块不提供虚拟化、排序或对象池。

命令支持 `void`、`Task`，最多一个业务参数，异步命令可接收 `CancellationToken`。不支持 `async void`。`CanExecute` 和执行状态控制按钮，Runtime 持续观察同一个任务。业务先保存，再展示奖励；取消确认时不提交。

命令可以等待关闭自己的窗口或祖先节点。关闭时解除绑定，VM 保留到命令结束再释放；外部关闭仍取消并等待命令。`runtime.WhenIdleAsync()` 同时等待延迟的 VM 释放。命令中等待 Runtime 关闭时先结束绑定，完整清理在该命令返回后完成。

退出前等待清理：

```csharp
await ui.DisposeAsync();
await runtime.DisposeAsync();
```

原始错误会记录并传播。UI 绑定和通知在引擎线程执行；直接改字段不会产生通知。

相关：[GUI 配置](mvvm-editor.md) · [MVVM API](../api/mvvm.md) · [生命周期](lifecycle.md)
