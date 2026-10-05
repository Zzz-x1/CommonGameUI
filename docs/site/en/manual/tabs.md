# Embedded tabs

## Scenario

Audio and graphics pages are authored together in one settings resource. Runtime selects what is shown; a category does not need a custom View.

## Resources and responsibilities

Settings → Body → Audio / Graphics. Body mounts Tab; both pages borrow existing widgets. Binding does not load or clone resources. Supply a custom Node when needed; otherwise the adapter uses a basic UINode.

## Usage

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

## Behavior and limits

This is the Godot Control overload; Unity takes RectTransform. Unselected pages stay alive and retain values. Hide clears selection without disposing pages. Names resolve within the container; missing or duplicate names fail. Switching to Current is an invalid core operation; project buttons may avoid submitting an unchanged choice. `SwitchAsync<T>` requires exactly one matching bound type; use names for basic Node pages. The Tab lock covers its mount. Disable selectors and related Close controls outside that mount during switching.

## Related chapters

[Godot tutorial](../getting-started/godot.md) · [Input](input-and-focus.md)
