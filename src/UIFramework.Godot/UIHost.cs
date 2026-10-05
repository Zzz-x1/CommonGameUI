using Godot;

namespace UIFramework.Godot;

public sealed class UIHost : IUIHost, IUILog
{
    private readonly int _thread = System.Environment.CurrentManagedThreadId;
    private readonly Viewport _viewport;
    private readonly LifetimeRunner _frames;
    public UIHost(Viewport viewport) { _viewport = viewport; _frames = LifetimeRunner.Get(viewport.GetTree()); }
    public Task NextFrameAsync() { VerifyAccess(); return _frames.NextFrameAsync(); }
    public void VerifyAccess()
    {
        if (System.Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("UI access must run on the Godot scene thread; dispatch through the host before calling.");
    }
    public object? CaptureFocus() { VerifyAccess(); return _viewport.GuiGetFocusOwner(); }
    public void Focus(IUIElement? element)
    {
        VerifyAccess();
        if (element == null) return;
        if (!(element is UIElement godot)) throw new ArgumentException("Expected a Godot display.", nameof(element));
        var control = UIElement.Enumerate(godot.Control).FirstOrDefault(c =>
            c.IsVisibleInTree() && c.FocusMode != Control.FocusModeEnum.None && (!(c is BaseButton b) || !b.Disabled));
        control?.GrabFocus();
    }
    public void RestoreFocus(object? focus)
    {
        VerifyAccess();
        // A disappeared focus target is a normal condition after closing its owning screen.
        if (focus is Control control && GodotObject.IsInstanceValid(control) && !control.IsQueuedForDeletion()
            && control.IsVisibleInTree() && control.FocusMode != Control.FocusModeEnum.None
            && (control is not BaseButton button || !button.Disabled)) control.GrabFocus();
    }
    public void Error(Exception exception, string context) => GD.PushError($"[UI] {context}\n{exception}");
}

public sealed class UIResourceLoader : IUIResourceLoader
{
    private readonly UIHost _host;
    public UIResourceLoader(UIHost host) => _host = host;
    public async Task<IUIResource> LoadAsync(string resource, CancellationToken token)
    {
        _host.VerifyAccess(); token.ThrowIfCancellationRequested();
        var error = ResourceLoader.LoadThreadedRequest(resource, "PackedScene");
        if (error != Error.Ok) throw new IOException($"Cannot request UI resource '{resource}': {error}.");
        // Godot cannot cancel a threaded request. Drain it before returning cancellation.
        while (ResourceLoader.LoadThreadedGetStatus(resource) == ResourceLoader.ThreadLoadStatus.InProgress)
            await _host.NextFrameAsync();
        var status = ResourceLoader.LoadThreadedGetStatus(resource);
        var loaded = ResourceLoader.LoadThreadedGet(resource);
        if (status != ResourceLoader.ThreadLoadStatus.Loaded)
            throw new IOException($"Cannot load UI resource '{resource}': {status}.");
        token.ThrowIfCancellationRequested();
        if (!(loaded is PackedScene scene)) throw new InvalidOperationException($"UI resource '{resource}' must be a PackedScene.");
        return new SceneResource(_host, resource, scene);
    }

    private sealed class SceneResource(UIHost host, string path, PackedScene scene) : IUIResource
    {
        private PackedScene? _scene = scene;
        public IUIElement Instantiate(IUIElement? mount)
        {
            host.VerifyAccess();
            if (!(mount is UIElement target)) throw new InvalidOperationException("A Godot scene needs an explicit Control mount.");
            var instance = (_scene ?? throw new ObjectDisposedException(nameof(SceneResource))).Instantiate();
            if (!(instance is Control control))
            {
                instance.Free();
                throw new InvalidOperationException($"UI resource '{path}' must have a Control root.");
            }
            control.Visible = false;
            try { target.Control.AddChild(control); return new UIElement(control, owned: true); }
            catch (Exception ex) { UI.Cleanup(ex, control.Free); throw; }
        }
        public void Dispose()
        {
            host.VerifyAccess();
            // Drop this owner's reference. Godot may share the same wrapper with other users.
            // Disposing that shared PackedScene would invalidate their managed reference.
            _scene = null;
        }
    }
}
