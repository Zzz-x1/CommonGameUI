# 游戏 UI 场景组合

以下代码放在项目异步业务方法中，容器已初始化，View、模型和实例列表由项目定义。

## 设置→画质→返回

```csharp
var settings = await modals.OpenAsync<SettingsView>();
var graphics = await modals.OpenAsync<GraphicsView>();
await modals.CloseAsync(graphics);
// settings remains the same instance.
```

关闭 graphics 后，原 settings 实例恢复交互。分类切换仍使用 Settings 内的 Tab。

## 大厅→背包→详情→回大厅

```csharp
var lobby = await pages.PushAsync<LobbyView>();
await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopToAsync(lobby);
```

PopToAsync 返回保存的大厅实例并清理中间页；普通返回用 PopAsync。

## 确认购买与奖励

```csharp
bool accepted = await modals.ShowDialogAsync<ConfirmView, bool>();
if (accepted)
{
    player.BuySelectedItem(); // Save currency/items before presentation.
    var reward = await modals.OpenAsync<RewardView>();
    // The reward close button closes this instance; purchase is not repeated.
}
```

确认后先保存购买结果，再打开奖励。奖励关闭按钮调用 CloseAsync(this)，恢复其下的商品详情。来源动画选择见[转场](../manual/transitions.md)。

## 两张装备对比卡

```csharp
if (comparisons.Count < 2)
    comparisons.Add(await windows.OpenAsync<CompareView>(new ItemArgs(selectedId)));
```

CompareView 继承 `UINode<ItemArgs>`，注册为 Multiple。第三张入口禁用；关闭后移除对应列表引用。

## 活动目标收起／恢复

```csharp
string selected = tabs.CurrentName!; // Project offers Fold only while selected.
await tabs.HideAsync();
await tabs.SwitchAsync(selected);
```

项目保存 selected，展开时恢复原页；操作期间禁用挂载外的收起与关闭按钮。

[Modal](../manual/modals-and-dialogs.md) · [Panel](../manual/panels.md) · [转场](../manual/transitions.md)
