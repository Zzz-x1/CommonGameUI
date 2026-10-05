# Timelines and custom animation

## Scenario

After rewards are saved, animate art, text, decoration and cards with a timeline. Skip reaches the visual endpoint only. Use project animation for an inventory icon flying into its details page.

## Resources and responsibilities

Save AnimationPlayer and non-looping enter/exit in Godot scenes. Unity uses native legacy Animation with non-looping AnimationClips, not Animator. Tracks change presentation only. The project restore callback restores all owned properties; the framework cannot infer arbitrary timeline snapshots.

## Usage

```csharp
Transition = GodotTransitions.Animation("AnimationPlayer", "enter", "exit", root =>
{
    root.Modulate = Colors.White;
    root.GetNode<Control>("Content").Scale = Vector2.One;
});
// For Unity, use UnityTransitions.Animation(componentPath, enterClip, exitClip, restore).
// Custom recipes implement these two contracts:
public sealed class ProjectTransition : IUITransition
{
    public IUITransitionRun Create(TransitionContext context) => new Run(context);
    private sealed class Run : IUITransitionRun
    {
        private readonly TransitionContext _context;
        public Run(TransitionContext context) { _context = context; }
        public Task PlayAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _context.ReportProgress(1);
            return Task.CompletedTask;
        }
        public void Restore() { /* Restore captured properties and free temporary visuals. */ }
    }
}
```

## Behavior and limits

The Godot snippet assumes authored white/unit-scale values; capture and restore actual project values otherwise. The custom Run illustrates the interfaces only, not a full performance. Each real Create must return independent execution state and install its start before yielding. Hide incoming presentation while waiting for layout to prevent a final-state flash. ReportProgress takes finite monotonic 0..1 playback time, not overshooting easing values. Restore is synchronous on success and failure, without replay; cancel stops the player, releases temporary visuals and restores properties. Skip, decorative pause and Partner rectangle calculations are project implementations in the Demo, not a universal core Skip API. Unity first-frame fixes and saved Demo tracks still require native verification.

## Related chapters

[Transitions](transitions.md) · [Samples](../samples/index.md)
