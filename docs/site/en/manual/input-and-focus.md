# Input and focus

Esc handles the top modal, then comparison windows, page history and finally quit confirmation.

## Usage

This return policy belongs to the project. Containers own their input locks; the shell gates external buttons and global keys.

```csharp
async Task BackAsync()
{
    if (modals.Current is ConfirmView confirm) confirm.Complete(false);
    else if (modals.Current is UINode modal) await modals.CloseAsync(modal);
    else if (compareWindows.Count > 0)
    {
        var window = compareWindows[^1];
        await windows.CloseAsync(window);
        compareWindows.Remove(window);
    }
    else if (pages.History.Count > 1) await pages.PopAsync();
    else await AskQuitAsync();
}
// Call only after the project verifies that the current layer is interactive.
```

## Key points

- Submit only when the current layer is interactive; wait during initialization/transitions.
- Maintain compareWindows and remove closed references. Cancel confirmation through a business answer.
- Input gates preserve business Disabled/interactable state; focus restores to valid visible usable controls.
- Configure keyboard navigation in the engine. Tutorials show complete input/task handling.

[Godot tutorial](../getting-started/godot.md) · [Unity tutorial](../getting-started/unity.md)
