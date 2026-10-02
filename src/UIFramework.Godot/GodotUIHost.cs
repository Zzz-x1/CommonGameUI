using Godot;

namespace UIFramework.Godot;

public sealed class GodotUIHost : IUIHost, IUILog
{
    private readonly int _thread = System.Environment.CurrentManagedThreadId;
    private readonly Viewport _viewport;
    public GodotUIHost(Viewport viewport) => _viewport = viewport;
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
        if (!(element is GodotUIElement godot)) throw new ArgumentException("Expected a Godot display.", nameof(element));
        var control = GodotUIElement.Enumerate(godot.Control).FirstOrDefault(c =>
            c.IsVisibleInTree() && c.FocusMode != Control.FocusModeEnum.None && (!(c is BaseButton b) || !b.Disabled));
        control?.GrabFocus();
    }
    public void RestoreFocus(object? focus)
    {
        VerifyAccess();
        // A disappeared focus target is a normal condition after closing its owning screen.
        if (focus is Control control && GodotObject.IsInstanceValid(control) && !control.IsQueuedForDeletion()
            && control.IsVisibleInTree() && control.FocusMode != Control.FocusModeEnum.None) control.GrabFocus();
    }
    public void Error(Exception exception, string context) => GD.PushError($"[UI] {context}\n{exception}");
}

public sealed class GodotSceneLoader : IUIResourceLoader
{
    private readonly GodotUIHost _host;
    public GodotSceneLoader(GodotUIHost host) => _host = host;
    public Task<IUIElement> LoadAsync(string resource, IUIElement? mount, CancellationToken token)
    {
        _host.VerifyAccess(); token.ThrowIfCancellationRequested();
        if (!(mount is GodotUIElement target)) throw new InvalidOperationException("A Godot scene needs an explicit Control mount.");
        var scene = ResourceLoader.Load<PackedScene>(resource) ?? throw new FileNotFoundException("UI scene was not found.", resource);
        var instance = scene.Instantiate();
        if (!(instance is Control control))
        {
            instance.Free();
            throw new InvalidOperationException($"UI resource '{resource}' must have a Control root.");
        }
        control.Visible = false;
        try
        {
            token.ThrowIfCancellationRequested();
            target.Control.AddChild(control);
            return Task.FromResult<IUIElement>(new GodotUIElement(control, owned: true));
        }
        catch { control.Free(); throw; }
    }
}
