# StackContainerNode：窗口历史

用于“只显示当前窗口，返回上一个历史窗口”的流程：大厅 → 背包 → 物品详情，或设置 → 画质 → 高级画质。业务节点可以叫 Window 或 View，不必继承专用 Page 类型。

## 资源结构

每个窗口使用独立 Prefab／PackedScene，业务脚本挂在各自资源根。所有窗口进入同一个 Stack 挂载：

```text
项目宿主
└─ PageMount              ← StackContainerNode 挂载

独立资源：SettingsWindow、GraphicsWindow、AdvancedGraphicsWindow
```

先注册各窗口资源，再按[创建与挂载](concepts.md#创建与挂载)创建 Stack。下例的 windows 为已经初始化的 `StackContainerNode`。

## 多级菜单与逐级返回

```csharp
var settings = await windows.PushAsync<SettingsWindow>();
await windows.PushAsync<GraphicsWindow>();
await windows.PushAsync<AdvancedGraphicsWindow>();

await windows.PopAsync(); // AdvancedGraphics → Graphics
await windows.PopAsync(); // Graphics → Settings
```

推进高级画质后，设置和画质窗口保留原实例并隐藏。返回一次销毁高级画质，恢复原画质实例；再次返回恢复原设置实例，其已保存于节点／控件中的状态继续保留。

## 一次回到指定窗口

```csharp
var lobby = await pages.PushAsync<LobbyView>();
await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopToAsync(lobby);
```

`PopToAsync` 清理目标之上的全部窗口，只恢复原大厅，不逐层显示中间窗口。目标使用具体实例，必须仍在同一栈的历史中；目标已经是当前窗口时正常完成。

## 行为与边界

| 操作／查询 | 行为 |
| --- | --- |
| PushAsync&lt;T&gt; | 新建窗口，完成切换后隐藏原窗口并加入历史 |
| PopAsync | 移除当前窗口，按注册策略销毁或缓存，恢复前一个实例 |
| PopToAsync(target) | 关闭目标之上的窗口，按注册策略销毁或缓存，直接恢复目标实例 |
| Current | 当前窗口；空栈时为 null |
| History | 从最早到当前的实例列表，包含当前窗口 |

- 稳定状态只有当前窗口显示；过场动画可能同时显示前后窗口。无需切换动画时，对 Push／Pop 传 `playAnimation: false`。
- `PopAsync` 可以移除最后一个窗口，此后栈为空；空栈再次 Pop 正常完成；连续提交两次 Pop 会依次返回两步，不合并请求。多级弹窗的首页返回通常关闭整个流程，见 [Modal 内的 Stack](modals-and-dialogs.md#单窗口多级菜单modal--stack)。
- 默认出栈销毁；KeepAlive 启用[关闭后缓存](resources.md#关闭后缓存)，缓存实例不属于历史。
- Stack 管历史和显隐，不自动创建遮罩或取得模态输入所有权。需要这些能力时组合 Modal。

[容器选择](concepts.md#按流程选择容器) · [生命周期](lifecycle.md) · [返回与焦点](input-and-focus.md)
