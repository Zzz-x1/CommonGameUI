# API Reference

## Entry points

| API | Typical use |
| --- | --- |
| [Core](core.md) | Context, Node, four Containers, Dialog, lifecycle and adapter contracts |
| [Godot](godot.md) | Control/PackedScene, AnimationPlayer, Tween recipes and backdrop |
| [Unity](unity.md) | RectTransform/Resources, legacy AnimationClip and backdrop |

Look up signatures/defaults by type; see the [Manual](../manual/concepts.md) for workflows.

## Choose an operation

| Intent | API |
| --- | --- |
| Open an independent window and hide/restore it | Panel.OpenAsync/HideAsync/ShowAsync/CloseAsync |
| Advance and return through pages | Stack.PushAsync/PopAsync/PopToAsync |
| Select authored categories | Tab.BindAsync/SwitchAsync(name)/HideAsync |
| Nest modals and return across layers | Modal.OpenAsync/CloseAsync/CloseToAsync |
| Await a business answer | Modal.ShowDialogAsync + Dialog.Complete |
| Prepare resources without displaying | Context.PreloadAsync |
| Choose animation by source | Transitions.Select(context => recipe) |
