# 节点、参数与依赖

装备详情接收装备 ID；注册回调传入名称表。业务脚本挂载在详情场景根节点上。

## 定义节点

Godot 示例资源含 Content/ItemName（Label）。Parameters 在初始化前已赋值：

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;
using EngineUI = UIFramework.Godot;

public sealed class ItemArgs
{
    public ItemArgs(string itemId) { ItemId = itemId; }
    public string ItemId { get; }
}
public partial class ItemDetailsView : EngineUI.UINode<ItemArgs>
{
    public IReadOnlyDictionary<string, string> Names { get; set; } = null!;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        this.GetControl<Label>("Content/ItemName").Text = Names[Parameters.ItemId];
        return Task.CompletedTask;
    }
}
```

## 注册与打开

```csharp
var itemNames = new Dictionary<string, string> { ["sword-01"] = "Iron Sword" };
ui.Register<ItemDetailsView>("res://ItemDetails.tscn", view => view.Names = itemNames);
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
```

## 要点

- `UINode<TArgs>` 接收创建参数；注册配置回调注入共享服务。
- 参数类型错误、名称表缺少 ID 时直接报错。
- 已有单实例窗口重开不传创建参数；刷新数据调用业务方法。

[窗口](panels.md) · [生成器](source-generator.md)
