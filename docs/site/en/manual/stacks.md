# StackContainerNode: window history

Use it to show the current window and return to a previous history window: lobby → inventory → item details, or settings → graphics → advanced graphics. Business nodes may be named Window or View; no dedicated Page base type is required.

## Resource structure

Each window uses a separate Prefab/PackedScene with its business script on the root. All windows enter the same Stack mount:

```text
Project host
└─ PageMount              ← StackContainerNode mount

Separate resources: SettingsWindow, GraphicsWindow, AdvancedGraphicsWindow
```

Register the window resources and follow [Create and mount](concepts.md#create-and-mount) to create the Stack. In the following example, windows is an initialized `StackContainerNode`.

## Multiple menu levels and stepwise Back

```csharp
var settings = await windows.PushAsync<SettingsWindow>();
await windows.PushAsync<GraphicsWindow>();
await windows.PushAsync<AdvancedGraphicsWindow>();

await windows.PopAsync(); // AdvancedGraphics → Graphics
await windows.PopAsync(); // Graphics → Settings
```

After pushing advanced graphics, settings and graphics retain their original instances and are hidden. Back disposes advanced graphics and restores the original graphics instance. Back again restores the original settings instance, retaining state already stored in its node/controls.

## Return directly to a window

```csharp
var lobby = await pages.PushAsync<LobbyView>();
await pages.PushAsync<InventoryView>();
await pages.PushAsync<ItemDetailsView>(new ItemArgs("sword-01"));
await pages.PopToAsync(lobby);
```

`PopToAsync` cleans all windows above the target and restores the original lobby without showing intermediate windows. Pass a concrete instance still in this stack’s history; targeting the current window completes without changes.

## Behavior and boundaries

| Operation/query | Behavior |
| --- | --- |
| PushAsync&lt;T&gt; | Creates or reuses a cached window; after switching, hides the previous window and adds the new one to history |
| PopAsync | Removes the current window, then disposes or caches it according to registration; restores the preceding instance |
| PopToAsync(target) | Closes windows above the target according to their cache policy and restores it directly |
| Current | Current window; null when the stack is empty |
| History | Instances from the earliest to the current window, including the current one |

- Only the current window is visible in a stable state; transitions may show both windows. Pass `playAnimation: false` to Push/Pop when no switching animation is needed.
- `PopAsync` can remove the last window, leaving an empty stack. Popping an empty stack succeeds. Two submitted Pop requests return two steps in order; they are not merged. Back from the first window of a modal menu usually closes the whole flow; see [Stack inside Modal](modals-and-dialogs.md#one-window-with-multiple-menu-levels-modal--stack).
- Popped windows are destroyed by default; KeepAlive enables [closed-instance caching](resources.md#closed-instance-caching). Cached windows are not history entries.
- Stack manages history and visibility. It does not create a backdrop or acquire modal input ownership; combine it with Modal for those features.

[Container selection](concepts.md#choose-by-the-flow) · [Lifecycle](lifecycle.md) · [Back and focus](input-and-focus.md)
