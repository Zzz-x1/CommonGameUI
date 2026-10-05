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

## Key points

- Initialize runs once; enter/exit may repeat. Hide retains; disposal ends the subtree.
- IsVisible is visibility, IsActive stable activity, and IsInteractive also includes input locks.
- Synchronous notifications are read-only toward the framework. Lifecycle cannot dispose its node/ancestors.
- Register AddLifecycle before attach. Idle removal stops notifications; initialized behavior still needs disposal.

[Signatures](../api/core.md) · [Errors and commit](async-and-errors.md)
