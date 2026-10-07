# TabContainerNode: embedded category switching

Use it for audio/graphics in Settings or equipment/materials in Inventory. All pages are authored in one resource; runtime switching selects existing nodes. Tab does not record switching history. Use [Stack](stacks.md) to return to a previous history page.

## Resource structure

```text
Settings                  ← parent business resource
├─ CategoryButtons        ← category buttons
└─ Body                   ← TabContainerNode mount
   ├─ Audio               ← existing page control
   └─ Graphics            ← existing page control
```

Body mounts the category pages; place selectors outside it. Pages can be basic controls or have business UINode scripts. A View class per page is not required.

## Usage

Put this example in the parent window's initialization/category operations. settings is a mounted parent node. Godot uses Control; Unity uses RectTransform.

```csharp
var tabs = await settings.CreateContainerAsync<TabContainerNode>("Categories", bodyControl);
await tabs.BindAsync("Audio", audioControl);
await tabs.BindAsync("Graphics", graphicsControl);
await tabs.SwitchAsync("Audio", playAnimation: false);
await tabs.SwitchAsync("Graphics");
await tabs.HideAsync();
await tabs.SwitchAsync("Audio");
string? category = tabs.CurrentName;
```

SwitchAsync("Graphics") hides Audio and shows Graphics, retaining both instances. HideAsync folds the current category and clears CurrentName. Another SwitchAsync restores a retained page.

## Behavior and boundaries

- Bind does not load or clone resources; categories need no custom View.
- Switch retains unselected pages; Hide clears selection but retains all instances.
- Duplicate bound names and missing switch targets fail. Switching to Current or hiding without a selection completes without replaying notifications or animation.
- If selectors are outside Body, disable them and Close together during switching.
- Switch does not add history. Returning to previously visited categories needs project-owned history, or a Stack-based flow.

[Container selection](concepts.md#choose-by-the-flow) · [Godot tutorial](../getting-started/godot.md) · [Input](input-and-focus.md)
