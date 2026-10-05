# 概念与职责

Node 是逻辑节点，可以关联界面，也可以只组织层级。Container 是提供打开、切换或返回能力的 Node。

业务 UI 脚本继承 `UIFramework.Unity.UINode`（MonoBehaviour）或 `UIFramework.Godot.UINode`（Control），挂在资源根节点。Core 的 `UINode` 用于 Root、容器和渲染无关扩展。容器返回实际业务脚本；共有身份和状态通过 `IUINode` 表达。

## 用法

UIContext 提供注册、资源服务和 Root。业务类可命名为 InventoryView；引擎控件通过 IUIElement 接入。

```csharp
var ui = new UIContext(host, loader);
var root = ui.Root;
var pages = new StackContainerNode();
await pages.AttachAsync(root, "Pages", pageMount);
ui.Register<LobbyView>("lobby-resource");
await pages.PushAsync<LobbyView>();
```

## 要点

- Panel 管独立窗口；Stack 管页面历史；Tab 切换内嵌页面；Modal 管嵌套弹窗。
- 容器本身也是 Node，操作直接调用容器 API。
- Root 只提供结构；业务挂载、控件与布局由项目定义。

[层级](hierarchy.md) · [API](../api/core.md)
