# Async, cancellation and errors

## Scenario

Opening resources, awaiting answers, transitioning and closing require waits. Registration, queries, configuration and answering need no async wrapper. Dispose Context before destroying engine mounts.

## Resources and responsibilities

Await operations from business code or send Tasks to a frame-owned observer. Preparation and playback receive CancellationToken; disposal cancels Node Lifetime.

## Usage

```csharp
await pages.PushAsync<InventoryView>(cancellationToken: token);
await modals.CloseToAsync(shop, playAnimation: false, cancellationToken: token);
await ui.DisposeAsync();
// When handling an exception at an application boundary, secondary cleanup
// failures remain available as original exceptions:
// IReadOnlyList<Exception> secondary = UIFailure.GetSecondaryExceptions(error);
```

## Behavior and limits

Same-container concurrency or reentry fails instead of queuing/retrying. Pre-commit failures restore old state; post-commit notification/cleanup failures do not undo the committed state. The framework logs context and propagates original errors without wrapping or false success. Cleanup failures cannot replace the primary; UIFailure exposes the original secondary errors. Normal Demo business code does not catch technical failures. If an application boundary catches and rethrows, use throw;. Cancellation remains cancellation, not a fabricated answer. External duplicate Dispose calls share their task/outcome; self/ancestor disposal inside lifecycle remains forbidden. Avoid Wait/Result, unobserved fire-and-forget and async void cleanup. Frame observers read results only after IsCompleted.

## Related chapters

[Lifecycle](lifecycle.md) · [Troubleshooting](../troubleshooting.md)
