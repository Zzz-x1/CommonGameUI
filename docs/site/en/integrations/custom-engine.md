# Custom engine integration

Implement these contracts to connect Core to another C# engine or resource backend.

## Responsibilities

- IUIHost: UI-thread checks and focus restoration.
- IUIElement: visibility, input, drawing order and instance disposal.
- IUIResourceLoader/IUIResource: load, instantiate and release resources.
- IUILog: log original errors.

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

## Connect

```csharp
var ui = new UIContext(host, loader, logger);
var pages = new StackContainerNode();
await pages.AttachAsync(ui.Root, "Pages", mountElement);
ui.Register<LobbyView>("lobby");
await pages.PushAsync<LobbyView>();
await ui.DisposeAsync();
```

host, loader, logger and mountElement are project implementations.

## Required behavior

- Instantiate hidden displays on the UI thread; borrowed instances do not destroy parent resources.
- Preserve business button state while gating input; restore drawing order and focus.
- Propagate original cancellation/create/cleanup failures and release native objects.
- Closing one instance must not release resources still held by another.

Custom animation implements IUITransition/IUITransitionRun. Backdrops implement IUIBackdropFactory/IUIBackdrop and observe dismiss tasks. See [Core API](../api/core.md).

Verify loading, cancellation, input, focus, animation restoration and resource release in the engine. Core tests do not replace native tests.
