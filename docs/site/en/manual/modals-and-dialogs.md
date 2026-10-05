# Modals and result dialogs

## Scenario

Open graphics settings over the settings modal, then return to settings. Await purchase confirmation and do not charge on an explicit cancel.

## Resources and responsibilities

Modal resources supply content; the container creates backdrops. Shared uses one backdrop, PerModal one per layer. `DialogNode<TResult>` supplies the answer; Modal returns it only after closure and cleanup.

## Usage

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

## Behavior and limits

Observe must retain and inspect Tasks raised by backdrop interaction. CloseAsync only closes the top; CloseTo retains an earlier instance and cleans all above it without activating intermediate layers. Answer buttons call Complete(true/false). Do not use Close as false: externally closing an unanswered dialog cancels its task. Complete is allowed only while interactive and only once. Configure DismissRequested to return a close Task, or Complete(false) then CompletedTask for a result dialog. With no handler, clicking the backdrop leaves it open. Simultaneous modal input ownership across separate Modal containers is unsupported and fails.

## Related chapters

[Complete tutorial](../getting-started/godot.md) · [Async](async-and-errors.md)
