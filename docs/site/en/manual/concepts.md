# Concepts and responsibilities

## Scenario

A client has a lobby page, a hideable inventory window and purchase confirmation. All are Nodes, but their flows need different Containers.

## Resources and responsibilities

UIContext owns the host, resource service, registrations and structural Root. UINode supplies identity, parent/child relationships and lifecycle; View is a business name, not a separate framework base. IUIElement represents an engine instance. The logical hierarchy is related to the widget tree but need not mirror every widget. A Container is itself a Node.

## Usage

```csharp
var ui = new UIContext(host, loader);
var root = ui.Root;
var pages = new StackContainerNode();
await pages.AttachAsync(root, "Pages", pageMount);
ui.Register<LobbyView>("lobby-resource");
await pages.PushAsync<LobbyView>();
```

## Behavior and limits

The adapter supplies host, loader and pageMount. Panel manages independent windows; Stack keeps page instances in history; Tab selects authored pages; Modal manages top-layer interaction and nested dialogs. `DialogNode<TResult>` supplies an awaited answer through Modal. There are no HUD/Overlay presets or mandatory Page/Sheet/Modal business base classes. The container determines how a View participates in the flow.

## Related chapters

[Hierarchy](hierarchy.md) · [API](../api/core.md)
