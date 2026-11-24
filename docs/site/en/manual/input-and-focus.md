# Input and focus

Esc handles the top modal, then comparison windows, page history and finally quit confirmation.

## Usage

This return policy belongs to the project. Containers own their input locks; the shell gates external buttons and global keys.

```csharp
async Task BackAsync()
{
    if (modals.Current is ConfirmView confirm) confirm.Complete(false);
    else if (modals.Current is SettingsFlowWindow flow && flow.Pages.History.Count > 1)
        await flow.Pages.PopAsync();
    else if (modals.Current is { } modal) await modals.CloseAsync(modal);
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

## Preferred focus and returning to a page

Drag a uGUI Selectable (Unity) or Control (Godot) into **Preferred Focus** on the window's root script. For a dynamic choice, override `GetPreferredFocus()` on the native node. The selected control must belong to the page.

When a Stack page takes focus for the first time, it selects its preferred control. Returning from equipment details first tries the original inventory row. If the row was removed or disabled, focus moves to the preferred control, then to the first usable control on the page. A cached window starts a new focus history on reopening. A modal has priority, and displaying a HUD preserves focus elsewhere. A tab switch transfers focus when it was inside the outgoing content and preserves it on the category button otherwise.

## Key points

- Submit only when the current layer is interactive; wait during initialization/transitions.
- Maintain compareWindows and remove closed references. Cancel confirmation through a business answer.
- If the current Modal hosts a menu history, pop its internal Stack first and close the Modal at the first window. SettingsFlowWindow is shown in [Modal + Stack](modals-and-dialogs.md#one-window-with-multiple-menu-levels-modal--stack).
- Input gates preserve business Disabled/interactable state; focus restores to valid visible usable controls.
- Configure keyboard navigation in the engine. Tutorials show complete input/task handling.

[Godot tutorial](../getting-started/godot.md) · [Unity tutorial](../getting-started/unity.md)
