# 游戏 UI 场景组合

以下代码放在项目异步业务方法中，容器已初始化，View、模型和实例列表由项目定义。

## 设置→画质→返回

```csharp
var settings = await modals.OpenAsync<SettingsView>();
var graphics = await modals.OpenAsync<GraphicsView>();
await modals.CloseAsync(graphics);
// settings remains the same instance.
```

两个资源只放面板内容，Shared 遮罩由 Modal 提供。音量／画面分类仍是 Settings 内的 Tab；画质独立弹窗不混进分类切换。

## 大厅→背包→详情→回大厅

```csharp
var lobby = await pages.PushAsync<LobbyView>();
await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopToAsync(lobby);
```

返回目标是保存的实例，不是字符串路由。中间页面销毁，大厅状态保留；普通返回使用 PopAsync。

## 确认购买→奖励→恢复商店

```csharp
bool accepted = await modals.ShowDialogAsync<ConfirmView, bool>();
if (accepted)
{
    player.BuySelectedItem(); // Save currency/items before presentation.
    var reward = await modals.OpenAsync<RewardView>();
    // The reward close button closes this instance; purchase is not repeated.
}
```

奖励窗口的动画来源是开窗时真实顶层节点，例如 PurchaseView；单封邮件保留正文，再打开同一个 RewardView。Transitions.Select 可据 Source 选择完整时间轴或短揭开。不额外保存上一窗口标签；不同动画不改变发奖结果。

## 两张装备对比卡

```csharp
if (comparisons.Count < 2)
    comparisons.Add(await windows.OpenAsync<CompareView>(new ItemArgs(selectedId)));
```

CompareView 继承 `UINode<ItemArgs>`，注册为 Multiple；第三张入口禁用或明确提示上限。关闭具体卡后从 comparisons 移除，不能用 TryGet 在多个实例中任选。上限属于项目，框架不硬编码两张。

## 活动目标收起／恢复

```csharp
string selected = tabs.CurrentName!; // Project offers Fold only while selected.
await tabs.HideAsync();
await tabs.SwitchAsync(selected);
```

保留原分类和控件值，项目保存恢复名称。操作时禁用分类挂载外的收起／关闭入口。输入政策、模型保存和动画跳过见对应 Manual，不增加新核心机制。

[Modal](../manual/modals-and-dialogs.md) · [Panel](../manual/panels.md) · [转场](../manual/transitions.md)
