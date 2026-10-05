# Concepts and responsibilities

Use a Node for a screen and a Container to define how it opens, switches and returns.

## Usage

UIContext provides registration, resource services and Root. A business class can be named InventoryView; IUIElement connects it to engine widgets.

```csharp
var ui = new UIContext(host, loader);
var root = ui.Root;
var pages = new StackContainerNode();
await pages.AttachAsync(root, "Pages", pageMount);
ui.Register<LobbyView>("lobby-resource");
await pages.PushAsync<LobbyView>();
```

## Key points

- Panel manages independent windows; Stack keeps page history; Tab selects embedded pages; Modal manages nested dialogs.
- Containers are Nodes. Call their operations directly.
- Root is structural. Your project supplies mounts, widgets and layout.

[Hierarchy](hierarchy.md) · [API](../api/core.md)
