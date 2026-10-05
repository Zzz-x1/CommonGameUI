# Unity 适配

## 接入方式与资源

先准备 netstandard2.1 核心 DLL，再添加本地 UPM 包，完整步骤见 [Unity Getting Started](../getting-started/unity.md)。当前不提供已发布的 Git UPM 地址，也未验证 Unity 内 Source Generator 工作流。

默认 UnityPrefabLoader 使用 Resources.`LoadAsync<GameObject>`，资源根为 RectTransform。注册使用 Resources 相对路径、不含扩展名，例如 UI/Inventory。Canvas／EventSystem 已激活，Canvas 上由 Create 配置 UnityFramePump 负责帧等待。

```csharp
var ui = UnityUI.Create(canvas, eventSystem);
ui.Register<InventoryView>("UI/Inventory");
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
pages.ChildTransition = UnityTransitions.SlideHorizontal(targetPath: "Content");
var inventory = await pages.PushAsync<InventoryView>();
Button close = inventory.GetComponent<Button>("Content/Close");
```

## 场景配置

使用 CanvasScaler 与 GraphicRaycaster，配置 EventSystem 的鼠标／键盘模块。空挂载不放可射线命中的 Graphic。模态资源根无全屏遮罩，Content Image 接受射线，遮罩由框架创建。CanvasGroup 作为输入门，按钮 interactable 仍由业务控制。

GetRect 与 `GetComponent<T>`(path) 访问真实控件；路径错误直接报错。内嵌 Tab 使用 RectTransform 绑定，借用已有实例。动态 Prefab 的逻辑销毁先失活、脱离挂载再 Destroy，避免延迟销毁污染兄弟顺序。

## 关闭与验证边界

Context 必须在 Canvas、EventSystem 和帧泵销毁前 await DisposeAsync。帧泵禁用会使未完成帧等待失败；不能在 OnDestroy 用 async void 伪装正常关闭。原生请求不能取消，资源释放只移除适配器引用，Resources 缓存由 Unity 管理。

使用原生 legacy AnimationClip，非 Animator。最新遮罩显隐与首帧修复仅离线编译，新增运行测试未执行；Demo 时间轴资产完整持久化验证仍未完成。当前编译环境 6000.3.24f1，不承诺 IL2CPP、移动端或其他版本已验证。

[Unity API](../api/unity.md) · [时间轴](../manual/timeline-animation.md)
