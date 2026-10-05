# Hierarchy and mounts

## Scenario

Mount lobby pages and modals under project-authored Pages and Dialogs areas. Inventory tabs use their Body region. Root does not create these mounts.

## Resources and responsibilities

A project can use Root → Pages / Windows / Dialogs, with project-defined names. Godot Control and Unity RectTransform are physical mounts. Engine scenes define stable rendering order and layout. Borrowing a mount does not transfer ownership of its scene.

## Usage

```csharp
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pagesControl);
var dialogs = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", dialogsControl);
var child = ui.Root.GetChild("Pages");
var parent = child.Parent;
```

## Behavior and limits

This snippet uses Godot extensions; Unity uses the same container helper with RectTransform. GetChild looks up a direct child by name, not a path; missing names fail. Sibling names must be unique. Attach establishes ownership once and waits for initialization; it is not a show or reparent operation. Ordinary nodes start hidden, containers visible. TransitionOrder changes temporary drawing priority during transitions and restores it afterwards; it does not replace the project’s stable layer layout.

## Related chapters

[Godot](../integrations/godot.md) · [Transitions](transitions.md)
