# Hierarchy and mounts

The logical hierarchy owns nodes; the engine hierarchy owns layout and drawing order.

## Usage

For example, place Pages, Windows and Dialogs under Root, with a category container inside inventory. Godot mounts use Control, Unity mounts use RectTransform. Names belong to the project; the example uses Godot extensions.

```csharp
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pagesControl);
var dialogs = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", dialogsControl);
var child = ui.Root.GetChild("Pages");
var parent = child.Parent;
```

## Key points

- Call Attach once to establish ownership and await initialization, not to show or reparent.
- GetChild looks up a direct child; missing or duplicate sibling names fail.
- Scenes define stable order. TransitionOrder applies only during a transition.

[Godot](../integrations/godot.md) · [Transitions](transitions.md)
