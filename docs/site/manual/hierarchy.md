# 层级与挂载

逻辑层级负责节点关系，引擎层级负责控件布局与绘制顺序。

## 用法

例如 Root 下放 Pages、Windows、Dialogs，背包内部再放分类容器。Godot 挂载用 Control，Unity 用 RectTransform；名称由项目决定。下例使用 Godot 扩展。

```csharp
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pagesControl);
var dialogs = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", dialogsControl);
var child = ui.Root.GetChild("Pages");
var parent = child.Parent;
```

## 要点

- Attach 只调用一次：建立父子关系并等待初始化，不用于显示或重挂载。
- GetChild 按名称查找直接子节点；名称缺失或兄弟重名报错。
- 稳定绘制顺序由场景定义；TransitionOrder 只影响转场期间。

[Godot](../integrations/godot.md) · [转场](transitions.md)
