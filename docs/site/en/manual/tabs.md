# Embedded tabs

Author audio and graphics categories together in Settings, then bind and select them at runtime.

## Usage

Structure: Settings → Body → Audio/Graphics. Body is the mount; both pages use existing controls. This example uses Godot; Unity takes RectTransform.

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

## Key points

- Bind does not load or clone resources; categories need no custom View.
- Switch retains unselected pages; Hide clears selection but retains all instances.
- Duplicate/missing names or switching to Current fails; buttons may avoid submitting an unchanged choice.
- If selectors are outside Body, disable them and Close together during switching.

[Godot tutorial](../getting-started/godot.md) · [Input](input-and-focus.md)
