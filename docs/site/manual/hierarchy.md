# 层级与挂载

逻辑层级负责节点关系，引擎层级负责控件布局与绘制顺序。

## 用法

例如 Root 下放 Pages、Windows、Dialogs，背包内部再放分类容器。Godot 挂载用 Control，Unity 用 RectTransform；名称由项目决定。下例使用 Godot 扩展。

```csharp
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages");
var dialogs = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", dialogsControl);
var child = ui.Root.GetChild("Pages");
var parent = child.Parent;
```

## 要点

- Attach 只调用一次：建立父子关系并等待初始化，不用于显示或重挂载。
- GetChild 按名称查找直接子节点；名称缺失或兄弟重名报错。
- 稳定绘制顺序由场景定义；TransitionOrder 只影响转场期间。

[Godot](../integrations/godot.md) · [转场](transitions.md)

## 自动挂载

省略 mount 时，两端会在最近的父节点显示对象下创建铺满父区域的空挂载；中间的纯结构节点不影响查找。Root 使用 Context 的根挂载：Unity 为 UI.Create 传入的 Canvas；Godot 为传入的 Control，或显式指定 UI.Create(owner, rootMount: control)。没有可用挂载时直接报错。

自动挂载随容器销毁。显式传入的 mount 只借用，不会随容器销毁；局部内容区或需要固定绘制顺序的窗口仍建议显式指定。自动挂载按创建顺序追加，不生成 Canvas 或项目层级预设。
