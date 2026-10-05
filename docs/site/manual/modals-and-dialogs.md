# 模态与结果弹窗

设置上打开画质弹窗，关闭后回到设置；购买时等待用户确认。

## 用法

弹窗资源只放内容。Modal 创建遮罩：Shared 共用一层，PerModal 每层独立。需要答案时继承 `DialogNode<TResult>`。Observe 是项目的任务观察入口。

```csharp
modals.ConfigureBackdrop(GodotBackdrops.Dim(Observe), ModalBackdropMode.Shared);
var settings = await modals.OpenAsync<SettingsView>();
var graphics = await modals.OpenAsync<GraphicsView>();
await modals.CloseAsync(graphics);
var shop = await modals.OpenAsync<ShopView>();
await modals.OpenAsync<PurchaseView>();
bool confirmed = await modals.ShowDialogAsync<ConfirmView, bool>();
if (confirmed) player.BuySelectedItem();
await modals.CloseToAsync(shop);
```

## 要点

- CloseAsync 只关闭顶层；CloseToAsync 保留更早目标并清理其上各层。
- 答案按钮调用 Complete(result)；ShowDialogAsync 等待关闭与清理后返回结果。
- Complete(false) 是明确取消；未回答就外部关闭会取消任务。
- Complete 只能在可交互时调用一次。遮罩点击关闭通过 DismissRequested 配置。

[完整教程](../getting-started/godot.md) · [异步](async-and-errors.md)
