# Source Generator

通过资源属性生成注册和类型化调用，减少重复代码。

## 用法

将生成器作为 Analyzer 引用；业务类使用公开、顶层、非泛型的原生节点，并挂载到所声明的场景根节点。Godot 脚本需要 partial。依赖注入使用手写资源注册的配置回调。

项目引用如下，按项目位置调整路径：

```xml
<ProjectReference Include="../../../src/UIFramework.Generators/UIFramework.Generators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

```csharp
using UIFramework;
using EngineUI = UIFramework.Godot;

namespace Game.UI
{
    public sealed class ItemArgs
    {
        public string ItemId { get; }
        public ItemArgs(string itemId) { ItemId = itemId; }
    }
    [UIResource("res://Inventory.tscn", CacheMode = NodeCacheMode.KeepAlive)]
    public partial class InventoryView : EngineUI.UINode { }
    [UIResource("res://Compare.tscn", WindowMode = WindowInstanceMode.Multiple)]
    public partial class CompareView : EngineUI.UINode<ItemArgs> { }
}
// In caller code: using UIFramework.Generated; using Game.UI;
// ui.RegisterGeneratedUI();
// var inventory = await windows.OpenInventoryViewAsync();
```

## 要点

- 注册导入 UIFramework.Generated；类型化调用导入节点业务命名空间。
- 已自动注册的类型不要再手写注册。
- 同名节点调用歧义时，限定 GeneratedUICalls 或使用核心泛型 API。
- 生成调用透传参数、playAnimation 和 cancellationToken；Unity 工作流尚未接入。

## 生成的签名

```csharp
// UIFramework.Generated.GeneratedUIRegistration:
void RegisterGeneratedUI(this UIContext ui)
// Game.UI.GeneratedUICalls, for the declared InventoryView:
Task<InventoryView> PushInventoryViewAsync(this StackContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this PanelContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
Task<InventoryView> OpenInventoryViewAsync(this ModalContainerNode container, bool playAnimation = true, CancellationToken cancellationToken = default)
```

[节点参数](nodes-and-parameters.md) · [API](../api/core.md)
