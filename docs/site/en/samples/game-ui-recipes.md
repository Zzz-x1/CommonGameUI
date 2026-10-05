# Game UI recipes

Put these snippets in project async methods with initialized containers. Views, models and instance lists belong to your project.

## Settings→graphics→back

```csharp
var settings = await modals.OpenAsync<SettingsView>();
var graphics = await modals.OpenAsync<GraphicsView>();
await modals.CloseAsync(graphics);
// settings remains the same instance.
```

Closing graphics restores the original settings instance. Categories still use the embedded Tab in Settings.

## Lobby→inventory→details→home

```csharp
var lobby = await pages.PushAsync<LobbyView>();
await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopToAsync(lobby);
```

PopToAsync restores the retained lobby and cleans intermediate pages. Use PopAsync for ordinary Back.

## Purchase confirmation and rewards

```csharp
bool accepted = await modals.ShowDialogAsync<ConfirmView, bool>();
if (accepted)
{
    player.BuySelectedItem(); // Save currency/items before presentation.
    var reward = await modals.OpenAsync<RewardView>();
    // The reward close button closes this instance; purchase is not repeated.
}
```

Save the purchase before opening rewards. Its Close button calls CloseAsync(this), restoring the details beneath it. See [Transitions](../manual/transitions.md) for source-based animation.

## Two equipment comparison cards

```csharp
if (comparisons.Count < 2)
    comparisons.Add(await windows.OpenAsync<CompareView>(new ItemArgs(selectedId)));
```

CompareView inherits `UINode<ItemArgs>` and registers as Multiple. Disable the third-card action and remove list references after closing.

## Fold and restore event goals

```csharp
string selected = tabs.CurrentName!; // Project offers Fold only while selected.
await tabs.HideAsync();
await tabs.SwitchAsync(selected);
```

Retain selected for restoration. Disable Fold/Close controls outside the mount during the operation.

[Modal](../manual/modals-and-dialogs.md) · [Panel](../manual/panels.md) · [Transitions](../manual/transitions.md)
