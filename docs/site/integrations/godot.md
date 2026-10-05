# Godot 适配

完整接入步骤见 [Godot Getting Started](../getting-started/godot.md)。

## 资源与控件

- PackedScene 根为 Control，注册使用 res:// 路径。
- 空挂载与全屏资源根设 MouseFilter.Ignore；实际面板接收输入。
- AttachToAsync／BindAsync 借用已有 Control；GetControl 按相对路径访问控件。

```csharp
var ui = GodotUI.Create(this);
ui.Register<InventoryView>("res://UI/Inventory.tscn");
var windows = await ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", mount);
var inventory = await windows.OpenAsync<InventoryView>();
Button close = inventory.GetControl<Button>("Content/Close");
```

## 要点

- 用 `ui.BindLifetime(sceneRoot)` 绑定场景，或 `node.BindLifetime(owner)` 绑定节点，见[生命周期](../manual/lifecycle.md)。
- 在场景线程、owner 进入树后调用 Create；loader／logger 可自行替换。
- Slide／Scale 使用独立内容包装层，避免布局 Container 重写动画位置。
- 动态场景实例由框架销毁；借用控件由原场景拥有。转移新控件所有权时显式传 owned: true。
- 释放场景前 await ui.DisposeAsync。后台加载不代表可以在后台线程操作控件。

[Godot API](../api/godot.md) · [资源管理](../manual/resources.md)
