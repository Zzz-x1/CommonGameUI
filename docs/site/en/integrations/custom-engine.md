# Custom engine integration

## When to write an adapter

Reuse window/history logic in another C# engine or replace an existing resource backend by implementing small contracts. Core should not reference engine types.

## Required interfaces

```csharp
public interface IUIElement : IAsyncDisposable
{
    void SetVisible(bool visible);
    void SetInteractive(bool interactive);
    void BringToFront();
    object CaptureOrder();
    void RestoreOrder(object order);
}
public interface IUIResourceLoader
{
    Task<IUIResource> LoadAsync(string resource, CancellationToken cancellationToken);
}
public interface IUIResource : IDisposable
{
    IUIElement Instantiate(IUIElement? mount);
}
public interface IUIHost
{
    void VerifyAccess();
    object? CaptureFocus();
    void Focus(IUIElement? element);
    void RestoreFocus(object? focus);
}
public interface IUILog
{
    void Error(Exception exception, string context);
}
```

Host verifies UI-thread access and captures restorable focus handles without touching destroyed objects. Element manages visibility, input and drawing order. Preserve business button conditions when gating input; RestoreOrder restores the CaptureOrder snapshot. DisposeAsync distinguishes owned dynamic instances from borrowed embedded controls; borrowed disposal must not destroy parent resources.

Loader awaits real resource acquisition. Resource.Instantiate synchronously creates a hidden display on the UI thread and attaches it to mount. Preserve original errors during cancellation, failure and release; clean partially created native objects. Resource release and instance destruction are separate: closing one must not release resources still used by another.

## Create a Context

```csharp
var ui = new UIContext(host, loader, logger);
var pages = new StackContainerNode();
await pages.AttachAsync(ui.Root, "Pages", mountElement);
ui.Register<LobbyView>("lobby");
await pages.PushAsync<LobbyView>();
await ui.DisposeAsync();
```

host, loader, logger and mountElement are project implementations. Core prescribes no resource format or business mounts. Adapter logs retain original errors; Context handles its own deduplication and failure flow.

## Animation and modal extensions

IUITransition.Create returns a separate IUITransitionRun per operation. PlayAsync awaits playback and cancellation; Restore synchronously restores presentation. IUIBackdropFactory.CreateAsync receives the dismiss callback; the host must observe each returned Task. IUIBackdrop Show/Hide honor the animation flag, and Restore restores stable visibility. Dismiss failures cannot be discarded.

## Verify the adapter

Use isolated scenes to test thread access, borrowed/owned disposal, shared loading, canceled late resources, input/focus restoration, sibling order, backdrop hiding, animation start before yielding, and simultaneous primary/cleanup failures. Native tests complement Core tests. Do not claim support for an unverified engine.

[Resources](../manual/resources.md) · [API](../api/core.md)
