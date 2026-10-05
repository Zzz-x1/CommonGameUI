# Game UI recipes

Put these snippets in project async methods with initialized containers. Views, models and instance lists belong to your project.

## Settings→graphics→back

```csharp
var settings = await modals.OpenAsync<SettingsView>();
var graphics = await modals.OpenAsync<GraphicsView>();
await modals.CloseAsync(graphics);
// settings remains the same instance.
```

Both resources contain panel content only; Modal supplies the Shared backdrop. Audio/graphics categories remain embedded Tabs in Settings; the separate graphics modal is not a category switch.

## Lobby→inventory→details→home

```csharp
var lobby = await pages.PushAsync<LobbyView>();
await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopToAsync(lobby);
```

The return target is a retained instance, not a string route. Intermediate pages are disposed while lobby state remains. Use PopAsync for ordinary Back.

## Purchase confirmation→rewards→shop

```csharp
bool accepted = await modals.ShowDialogAsync<ConfirmView, bool>();
if (accepted)
{
    player.BuySelectedItem(); // Save currency/items before presentation.
    var reward = await modals.OpenAsync<RewardView>();
    // The reward close button closes this instance; purchase is not repeated.
}
```

The reward animation source is the actual top node when opened, such as PurchaseView. Keep a single-mail body open before displaying the same RewardView. Transitions.Select chooses a full timeline or short reveal from Source. Do not store extra previous-window labels; animation choices do not change rewards.

## Two equipment comparison cards

```csharp
if (comparisons.Count < 2)
    comparisons.Add(await windows.OpenAsync<CompareView>(new ItemArgs(selectedId)));
```

CompareView inherits `UINode<ItemArgs>` and registers as Multiple. Disable the third-card action or show the limit. Remove references when closing cards; TryGet cannot arbitrarily choose among multiple instances. The two-card limit belongs to the project, not Core.

## Fold and restore event goals

```csharp
string selected = tabs.CurrentName!; // Project offers Fold only while selected.
await tabs.HideAsync();
await tabs.SwitchAsync(selected);
```

Retain page instances and values, storing the restoration name in the project. Disable Fold/Close controls outside the Tab mount during the operation. Input policy, model persistence and skip behavior use the existing Manual contracts without another core mechanism.

[Modal](../manual/modals-and-dialogs.md) · [Panel](../manual/panels.md) · [Transitions](../manual/transitions.md)
