# Troubleshooting and limits

## Why is an attached node invisible?

Attaching an ordinary UINode initializes it but starts hidden. Commit visibility with the appropriate Open/Push/Switch operation. Root is structural and creates no Canvas or Control. Check live mounts, layout sizes and project layer order.

## Why are buttons blocked?

Godot empty mounts use MouseFilter=Ignore; Unity empty mounts have no raycastable Graphic. Only the top modal interacts; IsInteractive=false during transitions. Tab locks do not cover external selectors. Explicitly gate those controls rather than concealing reentry with try/catch.

## Why does animation flash at the start?

Sample the start before yielding. Do not expose the authored preview endpoint while awaiting layout. Hide incoming presentation during layout, then restore as the timeline starts. Restore every owned track. Use wrappers for Godot Slide/Scale so layout Containers do not overwrite animated positions.

## Why do registration or generated calls fail to compile?

Use a factory for service constructors. Reference the generator as an Analyzer. Import UIFramework.Generated for registration and the node’s business namespace for typed calls. Qualify GeneratedUICalls or use generic APIs when two imported namespaces contain same-named nodes. The Unity generator workflow is not guaranteed.

## Why are arguments rejected on reopening?

Panel defaults to Single. An existing instance cannot accept new creation arguments on reopen. Refresh via a business method or Close and recreate. Register Multiple explicitly and retain instance references for multiple windows.

## Why is cancellation delayed?

Native resource requests cannot be canceled directly. The framework awaits completion and cleans late resources. Keep observing the task; do not block or ignore cancellation. Explicit business Cancel calls Dialog Complete(false); external unanswered closure is task cancellation.

## Is every engine/platform supported?

Core is renderer-independent, but each engine needs an adapter. Godot currently uses 4.7.2 .NET. Recent Unity changes have offline compilation only; native Demo timeline asset verification remains pending. Mobile, IL2CPP and arbitrary engine versions are not verified.

There is no widget/layout system, default dynamic-resource Tab workflow, automatic return policy, global input queue, pool budget, runtime reparenting, binding framework, editor configuration or Analyzer diagnostics. Source Generator does not mean a custom Analyzer is included.

[Async errors](manual/async-and-errors.md) · [API](api/index.md)
