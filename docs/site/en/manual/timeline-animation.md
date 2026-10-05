# Timelines and custom animation

Save rewards first, then animate art, text and cards. Skipping changes presentation, never grants rewards again.

## Usage

Godot uses AnimationPlayer; Unity supports Timeline/PlayableDirector and legacy Animation/AnimationClip. Tracks affect presentation only; restore resets every owned property.

```csharp
Transition = GodotTransitions.Animation("AnimationPlayer", "enter", "exit", root =>
{
    root.Modulate = Colors.White;
    root.GetNode<Control>("Content").Scale = Vector2.One;
});
```

## Unity Timeline

Lobby art, welcome text and buttons can use separate Animation tracks. Add `Transitions/Enter` and `Transitions/Exit` under the Prefab root. Each has a PlayableDirector, a finite Timeline asset and local track bindings. Disable **Play On Awake**.

Capture every property your tracks write, then configure the transition in node initialization:

```csharp
var root = this.GetRect();
var rects = root.GetComponentsInChildren<RectTransform>(true)
    .ToDictionary(r => r, r => (r.anchoredPosition, r.localScale, r.localRotation));
var groups = root.GetComponentsInChildren<CanvasGroup>(true)
    .ToDictionary(g => g, g => g.alpha);
Transition = UnityTransitions.Timeline("Transitions/Enter", "Transitions/Exit", _ =>
{
    foreach (var pair in rects)
    {
        pair.Key.anchoredPosition = pair.Value.anchoredPosition;
        pair.Key.localScale = pair.Value.localScale;
        pair.Key.localRotation = pair.Value.localRotation;
    }
    foreach (var pair in groups) pair.Key.alpha = pair.Value;
});
```

Playback starts at zero before the first frame wait and uses unscaled time. Completion, cancellation and failure stop the graph, restore director settings and invoke your restore callback. A bound owner destroyed during playback cancels the transition; an unrelated external stop remains an error.

`playAnimation: false` skips playback. For different sources or reduced motion, use [Transitions.Select](transitions.md). Install `com.unity.timeline` to author Timeline assets; the runtime adapter depends only on Unity's Director module. Tracks must remain presentation-only and must not hide the framework root or perform purchases, saves or rewards.

## Key points

- Custom transitions await their engine Host.NextFrameAsync, then check the cancellation token. Do not attach frame waits to controls that may be destroyed.
- The example restores white/unit scale. Capture actual authored values when they differ.
- Apply the start before yielding; hide incoming presentation while awaiting layout to avoid flashing.
- Custom animations implement IUITransition/IUITransitionRun, capturing independent state per Create. Restore cleans synchronously after success or failure.
- ReportProgress uses monotonic 0..1 time. Skip and Partner image transitions are project code; see the Demo.

[Transitions](transitions.md) · [Samples](../samples/index.md)
