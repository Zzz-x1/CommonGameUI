# Lifecycle

Bind once, refresh on each entry, pause decoration while inactive and resume it on activation.

## Phases

| Phase | Responsibility |
| --- | --- |
| OnInitializeAsync | Initial attach: bind controls and events |
| OnEnteringAsync/OnExitingAsync | Awaited preparation before entry/exit |
| OnEntered/OnExited | Synchronous post-commit notifications |
| OnActivated/OnDeactivated | Start/pause continuous presentation |
| OnVisibilityChanged | Effective visibility changes |
| OnDisposeAsync | Release business-owned resources |

## Example: refresh profile and pause decoration

The Godot resource contains Content/PlayerName (Label) and Decoration (AnimationPlayer with looping idle):

```csharp
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;
using EngineUI = UIFramework.Godot;

public sealed class PlayerProfile
{
    public string Nickname { get; set; } = "Traveller";
}
public partial class ProfileView : EngineUI.UINode
{
    public PlayerProfile Profile { get; set; } = null!;
    private AnimationPlayer _decoration = null!;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _decoration = this.GetRoot().GetNode<AnimationPlayer>("Decoration");
        _decoration.Play("idle");
        _decoration.Active = false;
        return Task.CompletedTask;
    }
    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        this.GetControl<Label>("Content/PlayerName").Text = Profile.Nickname;
        return Task.CompletedTask;
    }
    protected override void OnActivated(StateChangeContext context) => _decoration.Active = true;
    protected override void OnDeactivated(StateChangeContext context) => _decoration.Active = false;
}
```

```csharp
ui.Register<ProfileView>("res://Profile.tscn", view => view.Profile = profile);
await pages.PushAsync<ProfileView>();
```

## Binding engine lifetimes

Let game UI close when its level object or scene is destroyed. Binding is synchronous configuration; it does not open the UI:

```csharp
// Unity: using UIFramework.Unity;
var objectLifetime = gameUI.BindLifetime(gameObject);
// Or bind the scene through a hidden scene-local GameObject:
var sceneLifetime = ui.BindLifetime(gameObject.scene);

// Godot: using UIFramework.Godot;
using EngineUI = UIFramework.Godot;
// A scene is represented by its root node.
var godotLifetime = gameUI.BindLifetime(sceneRoot);
```

- A Node binding cleans up its subtree. A Context/Root binding also releases Context resources.
- Unity hides both its scene anchor and persistent observer in the Hierarchy. Godot adds no scene nodes.
- Destruction triggers cleanup; disabling, hiding or removing from the hierarchy does not. Godot QueueFree also triggers it.
- Native scripts automatically bind to their component lifetime; one additional explicit owner binding is allowed. `binding.Dispose()` disconnects it, preserving the node and the supplied engine object.
- Cleanup starts on a subsequent engine frame. Do not assume Destroy/Free completes it synchronously.
- `binding.Completion` waits for cleanup, or completes immediately when explicitly disconnected. For normal closure, still await the container operation or DisposeAsync.
- Binding does not navigate to the main menu. Automatic cleanup plays no exit transition and cancels active operations. Observe all operation tasks started by your project.
- OnDispose releases business resources and must not depend on destroyed controls. Callback errors propagate unchanged. Close UI before unloading when cleanup needs controls or an exit animation.
- Still await ui.DisposeAsync before application exit; asynchronous cleanup cannot continue after the engine loop stops.

## Key points

- Initialize runs once; enter/exit may repeat. Hide retains; disposal ends the subtree.
- Native scripts expose UIName / IsUIVisible to avoid engine name collisions. IsActive describes stable activity; IsInteractive also includes input locks.
- Synchronous notifications are read-only toward the framework. Lifecycle cannot dispose its node/ancestors.
- Register AddLifecycle before attach. Idle removal stops notifications; initialized behavior still needs disposal.

[Signatures](../api/core.md) · [Errors and commit](async-and-errors.md)

KeepAlive close exits and deactivates without repeating initialization or canceling Lifetime. Reopening updates arguments and sets `IsCacheReuse` to true; final release runs Dispose. See [closed-instance caching](resources.md#closed-instance-caching).
