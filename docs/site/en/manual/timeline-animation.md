# Timelines and custom animation

Save rewards first, then animate art, text and cards. Skipping changes presentation, never grants rewards again.

## Usage

Godot scenes store non-looping AnimationPlayer tracks; Unity uses legacy Animation/AnimationClip. Tracks affect presentation only; restore resets every owned property.

```csharp
Transition = GodotTransitions.Animation("AnimationPlayer", "enter", "exit", root =>
{
    root.Modulate = Colors.White;
    root.GetNode<Control>("Content").Scale = Vector2.One;
});
```

## Key points

- The example restores white/unit scale. Capture actual authored values when they differ.
- Apply the start before yielding; hide incoming presentation while awaiting layout to avoid flashing.
- Custom animations implement IUITransition/IUITransitionRun, capturing independent state per Create. Restore cleans synchronously after success or failure.
- ReportProgress uses monotonic 0..1 time. Skip and Partner image transitions are project code; see the Demo.

[Transitions](transitions.md) · [Samples](../samples/index.md)
