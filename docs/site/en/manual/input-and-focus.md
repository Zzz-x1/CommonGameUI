# Input and focus

## Scenario

Esc handles the top modal, then comparison windows, page history and finally lobby quit confirmation. Category switching also disables selectors and Close outside its mount.

## Resources and responsibilities

Containers lock their managed displays. Modal operations also temporarily block Context input and commit modal ownership. The shell handles cross-container controls, global keys and return priority.

## Usage

```csharp
async Task BackAsync()
{
    if (modals.Current is ConfirmView confirm) confirm.Complete(false);
    else if (modals.Current is UINode modal) await modals.CloseAsync(modal);
    else if (compareWindows.Count > 0) await windows.CloseAsync(compareWindows[^1]);
    else if (pages.History.Count > 1) await pages.PopAsync();
    else await AskQuitAsync();
}
// Call only after the project verifies that the current layer is interactive.
```

## Behavior and limits

This is project return policy. Maintain compareWindows and remove references after closure. Answer confirmation using business cancel policy, never converting technical errors into answers. IsInteractive includes locks; reject input during initialization/transitions or on inactive layers. Adapters restore mouse/focus configuration without overwriting business Disabled/interactable state. Focus restores only to a valid visible usable target. Configure native keyboard navigation in the engine. There is no global input queue, return policy or automatic Esc binding. See tutorials for complete gating and task observation.

## Related chapters

[Godot tutorial](../getting-started/godot.md) · [Unity tutorial](../getting-started/unity.md)
