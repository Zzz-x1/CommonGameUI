# Unity 适配

完整接入步骤见 [Unity Getting Started](../getting-started/unity.md)。

## 资源与控件

- Prefab 根为 RectTransform，保存在 Resources 下，注册路径不带扩展名。
- 挂载和资源根不放可命中的全屏 Graphic；弹窗遮罩由 Modal 创建。
- Tab 绑定既有 RectTransform，不克隆。GetRect／GetComponent 访问真实控件。

```csharp
var ui = UnityUI.Create(canvas, eventSystem);
ui.Register<InventoryView>("UI/Inventory");
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
pages.ChildTransition = UnityTransitions.SlideHorizontal(targetPath: "Content");
var inventory = await pages.PushAsync<InventoryView>();
Button close = inventory.GetComponent<Button>("Content/Close");
```

## 生命周期与动画

- Create 配置隐藏且跨场景存活的帧泵；用 BindLifetime 绑定场景或 GameObject，详见[生命周期](../manual/lifecycle.md)。
- 输入门保留按钮的业务 interactable；键盘导航由 EventSystem 配置。
- 支持 Timeline／PlayableDirector 和非循环 legacy AnimationClip，均按未缩放时间播放。
- Resources 请求不可直接取消；释放框架引用不立即清空引擎缓存。

## 验证状态

当前环境为 Unity 6000.3.24f1 / uGUI 2.0.0。客户端、适配器、生命周期及已保存 Timeline／AnimationClip 资源通过 31 项原生 Play Mode 回归。MVVM 编辑器 Undo／Prefab Override 与预览通过验证。独立入门教程仍仅离线编译，Unity Source Generator 工作流尚未接入。

[Unity API](../api/unity.md) · [时间轴](../manual/timeline-animation.md)
