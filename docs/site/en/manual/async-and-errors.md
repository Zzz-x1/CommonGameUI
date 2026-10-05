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

## Key points

- Same-container concurrency/reentry fails without automatic queuing.
- Pre-commit failures restore old state; post-commit notification/cleanup failures retain committed state.
- Original errors are logged and propagated. UIFailure exposes secondary cleanup errors; cancellation stays cancellation.
- Avoid Wait/Result and unobserved tasks. Frame observers read only completed results.
- External duplicate Dispose shares its outcome. Lifecycle callbacks cannot dispose their node/ancestors.

[Lifecycle](lifecycle.md) · [Troubleshooting](../troubleshooting.md)
