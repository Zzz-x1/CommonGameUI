# 节点、参数与依赖

## 使用场景

打开某件装备的详情需要装备 ID；背包刷新需要玩家模型。创建参数与服务依赖分开表达。

## 资源与职责

详情场景只负责控件。`UINode<TArgs>`.Parameters 保存创建参数；构造工厂注入服务。普通 Node 不要求泛型参数，只有确实需要时才使用。

## 使用代码

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

## 行为与限制

参数在初始化前赋值，类型错误或缺少必需参数直接报错。Panel 的单实例窗口首次创建可带参数，已有实例重开不能传非 null 创建参数；数据变化调用业务方法。没有通用数据绑定、MVVM 或 DI 容器，已有服务可以通过普通构造函数接入。业务模型不随 View 隐藏或销毁而丢失。

## 相关章节

[窗口](panels.md) · [生成器](source-generator.md)
