# Godot 适配

## 项目结构

使用 [Godot Getting Started](../getting-started/godot.md) 的两个 ProjectReference。业务资源根必须为 Control；PackedScene 动态实例由框架拥有，借用的挂载与内嵌 Tab 控件由原场景拥有。资源路径为 `res://…tscn`。

```csharp
var ui = GodotUI.Create(this); // owner must be inside the tree, on the scene thread.
ui.Register<InventoryView>("res://UI/Inventory.tscn");
var windows = await ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", mount);
var inventory = await windows.OpenAsync<InventoryView>();
Button close = inventory.GetControl<Button>("Content/Close");
```

节点 AttachToAsync 与 Control 版 BindAsync 借用已有实例，不加载资源；GetControl 返回根，`GetControl<T>`(path) 按相对路径查询控件，缺失或类型错误直接报错。不要把逻辑 View 当 Godot Node 脚本挂到资源。

## 布局、输入与动画

空结构挂载与全屏资源根用 MouseFilter.Ignore；实际面板和遮罩接受鼠标。按钮保留 FocusMode，项目控制 Disabled；适配器输入门不会覆盖此业务状态。

Scale／Slide 的目标不要直接受布局 Container 控制，使用独立内容包装层。AnimationPlayer 初始轨道在首次等待前采样；若项目演出等待布局，先遮挡进入页面再播放。不要用长延时隐藏布局问题。

## 资源与关闭

GodotSceneLoader 后台取得 PackedScene，实例化在场景线程。底层请求不能直接取消，取消后等待其结束。释放只归还框架引用，不能承诺立即释放 GPU 内存。使用 GodotUI.Create(owner, loader: customLoader, logger: customLogger) 替换服务。

正常关闭先 await ui.DisposeAsync，再释放场景；借用控件不由框架单独 QueueFree。自己创建并转移所有权的控件显式使用 GodotUIElement(control, owned: true)。当前不自动调度后台线程调用到主线程。

[Godot API](../api/godot.md) · [生命周期](../manual/lifecycle.md)
