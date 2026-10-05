# 节点、参数与依赖

装备详情用创建参数传入装备 ID；玩家模型等服务通过构造函数注入。

## 用法

继承 `UINode<TArgs>`，通过 Parameters 读取参数。带服务的 View 使用 Register(factory, resource) 注册。

```csharp
public sealed record ItemArgs(string ItemId);
public sealed class ItemDetailsView : UINode<ItemArgs>
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        string id = Parameters.ItemId;
        return Task.CompletedTask;
    }
}
// Inside an async method, after registration:
// ui.Register(() => new InventoryView(playerModel), "inventory-resource");
// await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
```

## 要点

- 参数在初始化前赋值；类型不符或缺少必需参数报错。
- 参数只用于创建。已有单实例窗口重开不传参数，刷新数据调用业务方法。
- 模型独立于 View，隐藏或销毁界面不应丢失玩家数据。

[窗口](panels.md) · [生成器](source-generator.md)
