# 概念与职责

Node 是逻辑节点，可以关联界面，也可以只组织层级。Container 是提供打开、切换或返回能力的 Node。

业务 UI 脚本继承 `UIFramework.Unity.UINode`（MonoBehaviour）或 `UIFramework.Godot.UINode`（Control），挂在资源根节点。Core 的 `UINode` 用于 Root、容器和渲染无关扩展。容器返回实际业务脚本；共有身份和状态通过 `IUINode` 表达。

## 按流程选择容器

业务类叫 Window、View 或 Page 都可以；根据显示和返回行为选择容器。

| 需求与场景 | 容器 | 主要操作 | 状态与历史 |
| --- | --- | --- | --- |
| 背包独立开关、两张装备对比卡同时显示 | [PanelContainerNode](panels.md) | Open／Hide／Show／Close | 独立管理窗口；没有返回历史 |
| 设置 → 画质 → 高级画质，每次显示一个，逐级返回 | [StackContainerNode](stacks.md) | Push／Pop／PopTo | 保留并隐藏历史窗口；返回恢复原实例 |
| 背包装备／材料分类，所有页已放在同一资源中 | [TabContainerNode](tabs.md) | Bind／Switch／Hide | 切换已绑定项；没有返回历史 |
| 商品详情上打开确认框，背景保留但不能操作 | [ModalContainerNode](modals-and-dialogs.md) | Open／Close／CloseTo／ShowDialog | 下层弹窗保持可见；顶层获得输入 |

**单窗口、多级历史使用 Stack；需要模态遮罩时，用一个 Modal 外壳承载这个 Stack。**

## 创建与挂载

UIContext 提供注册、资源服务和默认 Root。项目创建容器，并将它挂载到自己的控件上。下例放在宿主初始化方法中：

```csharp
var ui = UI.Create(this);
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
```

Godot 引入 `UIFramework` 和 `UIFramework.Godot`，pageMount 为 `Control`；Unity 引入 `UIFramework` 和 `UIFramework.Unity`，pageMount 为 `RectTransform`。完整宿主见 [Godot 入门](../getting-started/godot.md)／[Unity 入门](../getting-started/unity.md)。

- 容器本身也是 Node，可以作为其他节点的子节点；操作直接调用容器 API。
- 打开资源前用 `ui.Register<T>(resource)` 注册业务类型。Tab 绑定已有控件时无需资源注册。
- Root 只提供结构；项目决定容器名称、挂载、布局和绘制顺序。
- 销毁容器或关闭其所属窗口时清理子树。异步操作需要等待，并由项目观察错误。

[层级](hierarchy.md) · [API](../api/core.md)
