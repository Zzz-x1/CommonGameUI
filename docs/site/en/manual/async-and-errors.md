# Async, cancellation and errors

Await opening, transitions, answers and closure. Keep registration, queries and configuration synchronous.

## Usage

Await tasks directly or observe them from the frame entry. Dispose Context before destroying engine mounts.

```csharp
await pages.PushAsync<InventoryView>(cancellationToken: token);
await modals.CloseToAsync(shop, playAnimation: false, cancellationToken: token);
await ui.DisposeAsync();
// When handling an exception at an application boundary, secondary cleanup
// failures remain available as original exceptions:
// IReadOnlyList<Exception> secondary = UIFailure.GetSecondaryExceptions(error);
```

## Open a page and continue after it closes

For example, the caller refreshes the lobby after the player closes inventory. `OpenAsync/PushAsync` waits only for opening; `WaitForCloseAsync` waits for the player or other code to end that opening:

```csharp
using UIFramework;

var inventory = await pages.PushAsync<InventoryView>();
await inventory.WaitForCloseAsync(token);
// Continue the caller's flow after inventory closes.
```

The same extension works for Panel, Stack and Modal in both engines. It includes exit animation, close notifications and cache return or disposal cleanup. Hide, covering and deactivation do not complete it. Closing into KeepAlive does complete it; Lifetime is not a substitute.

The call captures the current opening session. Cache reopening starts a new session without changing old waits. Repeated Single opens share a session. Cancellation stops only this wait; it does not close the window or cancel other waits. Failed close preparation leaves the window open and the wait pending. Cleanup errors after closure commits propagate unchanged. Direct disposal and parent cleanup also finish the wait after this window's cleanup.

Wait from an external flow, not from the window's own, ancestor or descendant active lifecycle. Root, structural containers and embedded Tabs not opened through Panel/Stack/Modal have no opening session. Use `ShowDialogAsync` when a business answer is required.

## Submit several close requests

Leaving equipment details closes both comparison windows. Requests queue immediately and close in order; `WhenAll` observes all outcomes.

```csharp
var closeLeft = windows.CloseAsync(left);
var closeRight = windows.CloseAsync(right);
await Task.WhenAll(closeLeft, closeRight);
```

## Key points

- Structural operations use FIFO within each container; different containers can run independently. Each task waits for its own animation and cleanup.
- Requesting the active operation’s container or an ancestor from its lifecycle still fails. Initializing and awaiting child containers is allowed.
- Cancellation before execution removes the request from execution. Active cancellation waits for restoration and cleanup. Parent disposal cancels and drains the queue.
- Ordinary failures affect only their request. Failed restoration of presentation, input, or ordering blocks further operations; explicitly dispose the container.
- Pre-commit failures restore old state; post-commit notification/cleanup failures retain committed state.
- Original errors are logged and propagated. UIFailure exposes secondary cleanup errors; cancellation stays cancellation.
- Avoid Wait/Result and unobserved tasks. Frame observers read only completed results.
- External duplicate Dispose shares its outcome. Lifecycle callbacks cannot dispose their node/ancestors.

[Lifecycle](lifecycle.md) · [Troubleshooting](../troubleshooting.md)
