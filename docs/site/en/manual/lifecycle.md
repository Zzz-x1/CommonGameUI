# Lifecycle

## Scenario

Character details bind controls once and refresh model data on return. Pause lobby decoration while a modal owns input, then resume it.

## Resources and responsibilities

Logical nodes are distinct from engine resources. Initialization happens on attach. Entering/Exiting are awaited pre-commit preparation; Entered/Exited are synchronous post-commit notifications. Activated/Deactivated report effective activity; VisibilityChanged reports effective visibility.

## Usage

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

## Behavior and limits

Initialize runs once; enter/exit can repeat. Hide and coverage retain the instance; Dispose ends its subtree. IsVisible includes ancestors, IsActive includes stable input ownership, and IsInteractive also includes temporary locks; animation locks are not business deactivation. Preparation failures restore uncommitted state. Failures in post-commit notification or cleanup retain committed state and propagate the original error. Notifications and selectors are read-only toward the framework; do not invoke mutations from them. A lifecycle callback cannot dispose its own node or ancestor. Add independent behaviors before attach using AddLifecycle(new MyBehavior()); subclass NodeLifecycle and override needed phases only. Addition is pre-attach only; removal is allowed while idle and Ready, stopping later notifications while preserving initialized behavior cleanup at disposal.

## Related chapters

[API phase signatures](../api/core.md) · [Failures](async-and-errors.md)
