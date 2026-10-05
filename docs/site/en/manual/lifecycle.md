# Lifecycle

Bind controls on initialization, refresh on entry, pause decoration on deactivation and release resources on disposal.

## Usage

Entering/Exiting are awaited preparation. Entered/Exited are synchronous notifications after commit.

```csharp
public sealed class LobbyView : UINode
{
    protected override Task OnInitializeAsync(CancellationToken token) => Task.CompletedTask;
    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        // Refresh from the project model; context.IsFirstEnter identifies first entry.
        return Task.CompletedTask;
    }
    protected override void OnActivated(StateChangeContext context) { /* Resume decoration. */ }
    protected override void OnDeactivated(StateChangeContext context) { /* Pause decoration. */ }
    protected override Task OnDisposeAsync() => Task.CompletedTask;
}
```

## Key points

- Initialize runs once; entry/exit may repeat. Hiding retains; Dispose ends the subtree.
- IsVisible is effective visibility, IsActive stable activity, and IsInteractive also considers input locks.
- Notifications cannot mutate containers or dispose the current node/ancestors.
- AddLifecycle registers behavior before attach. RemoveLifecycle is allowed while idle; initialized behavior still receives disposal.
- See [Async and errors](async-and-errors.md) for failure-state rules.

[API phase signatures](../api/core.md) · [Failures](async-and-errors.md)
