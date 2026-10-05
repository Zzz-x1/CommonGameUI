# 节点、参数与依赖

装备详情接收装备 ID；名称表通过构造函数注入。创建参数与服务依赖分开处理。

## 定义节点

Godot 示例资源含 Content/ItemName（Label）。Parameters 在初始化前已赋值：

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;

public sealed class ItemArgs
{
    public ItemArgs(string itemId) { ItemId = itemId; }
    public string ItemId { get; }
}
public sealed class ItemDetailsView : UINode<ItemArgs>
{
    private readonly IReadOnlyDictionary<string, string> _names;
    public ItemDetailsView(IReadOnlyDictionary<string, string> names) { _names = names; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        this.GetControl<Label>("Content/ItemName").Text = _names[Parameters.ItemId];
        return Task.CompletedTask;
    }
}
```

## 注册与打开

```csharp
var itemNames = new Dictionary<string, string> { ["sword-01"] = "Iron Sword" };
ui.Register(() => new ItemDetailsView(itemNames), "res://ItemDetails.tscn");
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
```

## 要点

- `UINode<TArgs>` 接收创建参数；工厂注册注入共享服务。
- 参数类型错误、名称表缺少 ID 时直接报错。
- 已有单实例窗口重开不传创建参数；刷新数据调用业务方法。

[窗口](panels.md) · [生成器](source-generator.md)
