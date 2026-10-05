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

public sealed class PlayerProfile
{
    public string Nickname { get; set; } = "Traveller";
}
public sealed class ProfileView : UINode
{
    private readonly PlayerProfile _profile;
    private AnimationPlayer _decoration = null!;
    public ProfileView(PlayerProfile profile) { _profile = profile; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _decoration = this.GetControl().GetNode<AnimationPlayer>("Decoration");
        _decoration.Play("idle");
        _decoration.Active = false;
        return Task.CompletedTask;
    }
    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        this.GetControl<Label>("Content/PlayerName").Text = _profile.Nickname;
        return Task.CompletedTask;
    }
    protected override void OnActivated(StateChangeContext context) => _decoration.Active = true;
    protected override void OnDeactivated(StateChangeContext context) => _decoration.Active = false;
}
```

```csharp
ui.Register(() => new ProfileView(profile), "res://Profile.tscn");
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
// A scene is represented by its root node.
var godotLifetime = gameUI.BindLifetime(sceneRoot);
```

- A Node binding cleans up its subtree. A Context/Root binding also releases Context resources.
- Unity hides both its scene anchor and persistent observer in the Hierarchy. Godot adds no scene nodes.
- Destruction triggers cleanup; disabling, hiding or removing from the hierarchy does not. Godot QueueFree also triggers it.
- One binding per node. `binding.Dispose()` disconnects it, preserving the node and the supplied engine object.
- Cleanup starts on a subsequent engine frame. Do not assume Destroy/Free completes it synchronously.
- `binding.Completion` waits for cleanup, or completes immediately when explicitly disconnected. For normal closure, still await the container operation or DisposeAsync.
- Binding does not navigate to the main menu. Automatic cleanup plays no exit transition and cancels active operations. Observe all operation tasks started by your project.
- OnDispose releases business resources and must not depend on destroyed controls. Callback errors propagate unchanged. Close UI before unloading when cleanup needs controls or an exit animation.
- Still await ui.DisposeAsync before application exit; asynchronous cleanup cannot continue after the engine loop stops.

## Key points

- Initialize runs once; enter/exit may repeat. Hide retains; disposal ends the subtree.
- IsVisible is visibility, IsActive stable activity, and IsInteractive also includes input locks.
- Synchronous notifications are read-only toward the framework. Lifecycle cannot dispose its node/ancestors.
- Register AddLifecycle before attach. Idle removal stops notifications; initialized behavior still needs disposal.

[Signatures](../api/core.md) · [Errors and commit](async-and-errors.md)
