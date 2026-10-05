# Source Generator

## 使用场景

大量业务节点需要资源注册和类型化调用，希望减少重复 Register 与泛型调用代码。仍保留手写工厂注入服务。

## 资源与职责

在消费项目添加 Analyzer 项目引用；不是把生成器当运行时 DLL。Godot Demo 使用此方式，Unity 尚未接入生成器工作流。公开、非泛型、顶层且可无参构造的业务类是这里的示例形状。

## 使用代码

```csharp
namespace Game.UI
{
    [UIResource("res://Inventory.tscn")]
    public sealed class InventoryView : UINode { }
    [UIResource("res://Compare.tscn", WindowMode = WindowInstanceMode.Multiple)]
    public sealed class CompareView : UINode { }
}
// In caller code: using UIFramework.Generated; using Game.UI;
// ui.RegisterGeneratedUI();
// var inventory = await windows.OpenInventoryViewAsync();
```

## 行为与限制

项目引用写为 `<ProjectReference Include=".../UIFramework.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`，路径按项目调整。注册扩展在 UIFramework.Generated；调用扩展在节点的业务命名空间。不同命名空间同名节点可并存，同时导入导致调用歧义时限定 Game.UI.GeneratedUICalls 或使用核心泛型 API。生成 Push／Panel Open／Modal Open；`UINode<TArgs>` 生成带参调用与 Panel 无参重开；`DialogNode<TResult>` 生成 Show…Async，透传 playAnimation 与 cancellationToken。没有动态 Tab 生成、绑定生成或自定义 Analyzer 诊断；依赖构造继续手写注册。

## 生成的签名示例

```csharp
// UIFramework.Generated.GeneratedUIRegistration:
void RegisterGeneratedUI(this UIContext ui)
// Game.UI.GeneratedUICalls, for the declared InventoryView:
Task<InventoryView> PushInventoryViewAsync(this StackContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this PanelContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this ModalContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
```

## 相关章节

[节点参数](nodes-and-parameters.md) · [API](../api/core.md)
