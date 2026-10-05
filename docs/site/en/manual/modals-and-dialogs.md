# Modals and result dialogs

Open graphics over settings and return to settings on close; await confirmation before purchasing.

## Usage

Modal resources contain content only. Shared uses one backdrop; PerModal uses one per layer. Inherit `DialogNode<TResult>` for an answer. Observe is the project task observer.

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

## Key points

- CloseAsync closes the top only; CloseToAsync retains an earlier target and cleans the layers above it.
- Answer buttons call Complete(result). ShowDialogAsync returns after closure and cleanup.
- Complete(false) is an explicit cancel answer; external unanswered closure cancels the task.
- Complete is allowed once while interactive. Configure DismissRequested for backdrop dismissal.

[Complete tutorial](../getting-started/godot.md) · [Async](async-and-errors.md)
