# 层级与挂载

## 使用场景

大厅和弹窗分别挂在项目场景的 Pages 与 Dialogs 区域；背包内的分类挂在其 Body。结构 Root 不自动生成这些挂载。

## 资源与职责

建议逻辑结构为 Root → Pages / Windows / Dialogs，其中名称完全由项目决定。Godot 的 Control 与 Unity 的 RectTransform 为真实挂载；引擎场景负责稳定层级与布局。借用挂载不改变资源所有权。

## 使用代码

```csharp
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pagesControl);
var dialogs = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", dialogsControl);
var child = ui.Root.GetChild("Pages");
var parent = child.Parent;
```

## 行为与限制

此片段使用 Godot 扩展；Unity 使用相同容器创建方法但传 RectTransform。GetChild 是直接子节点按名查找，不是路径解析；缺失直接报错。兄弟节点名称必须唯一。Attach 是一次性建立所有权并等待初始化，不是每次显示或重挂载。普通节点默认隐藏，容器默认可见。转场中的临时绘制优先级使用 TransitionOrder，完成后恢复，不能替代项目长期层级。

## 相关章节

[Godot](../integrations/godot.md) · [转场](transitions.md)
