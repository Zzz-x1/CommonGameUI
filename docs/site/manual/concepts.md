# 概念与职责

## 使用场景

一个游戏客户端同时有大厅页面、可独立隐藏的背包窗口和购买确认。它们都是 Node，但交互流程不同，用不同 Container 管理。

## 资源与职责

UIContext 持有宿主、资源服务、注册和结构 Root。UINode 表示逻辑身份、父子关系及生命周期；View 是业务命名，不是另一种框架基类。IUIElement 是引擎实例句柄；逻辑树与引擎控件树有关联，但不要求逐控件镜像。Container 自身也是 Node。

## 使用代码

```csharp
var ui = new UIContext(host, loader);
var root = ui.Root;
var pages = new StackContainerNode();
await pages.AttachAsync(root, "Pages", pageMount);
ui.Register<LobbyView>("lobby-resource");
await pages.PushAsync<LobbyView>();
```

## 行为与限制

host、loader 和 pageMount 由引擎适配提供。Panel 管独立窗口；Stack 管保留实例的页面历史；Tab 管已有页面选择；Modal 管顶层独占交互及嵌套弹窗。`DialogNode<TResult>` 是可等待答案的节点，可由 Modal 打开。不内置 HUD／Overlay，不要求 Page／Sheet／Modal 业务基类；同一 View 是否作为页面或弹窗由容器使用场景决定。

## 相关章节

[层级](hierarchy.md) · [API](../api/core.md)
