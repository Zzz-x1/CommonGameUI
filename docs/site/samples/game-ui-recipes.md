# 游戏 UI 场景组合

以下代码放在项目异步业务方法中，容器已初始化，View、模型和实例列表由项目定义。

## 设置→画质→高级画质→逐级返回

一个 Modal 外壳承载一个 Stack，各窗口是独立资源。SettingsFlowWindow 的完整初始化脚本见 [Modal + Stack](../manual/modals-and-dialogs.md#单窗口多级菜单modal--stack)。

```csharp
var window = await modals.OpenAsync<SettingsFlowWindow>();
await window.Pages.PushAsync<GraphicsWindow>();
await window.Pages.PushAsync<AdvancedGraphicsWindow>();
await window.Pages.PopAsync(); // AdvancedGraphics → Graphics
await window.Pages.PopAsync(); // Graphics → Settings
```

稳定状态每次只显示当前窗口；Pop 每次回退一层，恢复原实例。回到设置后再次返回，项目关闭外层 Modal。设置窗口中的音量／画面平级分类仍使用 Tab，不加入多级窗口历史。

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
