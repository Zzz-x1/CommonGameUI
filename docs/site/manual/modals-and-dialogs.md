# 模态与结果弹窗

## 使用场景

设置弹窗上打开画质弹窗，关闭画质后返回设置；购买时等待确认，明确取消则不扣款。

## 资源与职责

弹窗资源只提供内容，遮罩由容器创建。Shared 使用共享遮罩，PerModal 为各层独立遮罩。`DialogNode<TResult>` 提交答案，Modal 等待关闭与清理后才返回它。

## 使用代码

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

## 行为与限制

Observe 必须保存并观察遮罩触发的 Task。CloseAsync 只关闭当前顶层，CloseTo 保留指定更早实例并清理其上各层，中间层不逐层激活。结果节点按钮调用 Complete(true/false)，不能只 Close 来表达 false；未回答就外部关闭会取消任务。Complete 只在有效交互期允许，重复完成报错。一般窗口可配置 DismissRequested 返回关闭 Task，结果弹窗可用 Complete(false) 后返回 CompletedTask；未设置则点击遮罩不关闭。不同 Modal 容器同时取得模态输入不受支持，会报错。

## 相关章节

[完整教程](../getting-started/godot.md) · [异步](async-and-errors.md)
