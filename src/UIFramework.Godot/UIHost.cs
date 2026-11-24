using Godot;

namespace UIFramework.Godot;

public sealed class UIHost : IUIHost, IUILog
{
    private readonly int _thread = System.Environment.CurrentManagedThreadId;
    private readonly Viewport _viewport;
    private readonly LifetimeRunner _frames;
    public Control? RootMount { get; }
    public UIHost(Viewport viewport, Control? rootMount = null) { RootMount = rootMount; _viewport = viewport; _frames = LifetimeRunner.Get(viewport.GetTree()); }
    public Task NextFrameAsync() { VerifyAccess(); return _frames.NextFrameAsync(); }
    public void VerifyAccess()
    {
        if (System.Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("UI access must run on the Godot scene thread; dispatch through the host before calling.");
    }
    public object? CaptureFocus() { VerifyAccess(); return _viewport.GuiGetFocusOwner(); }
    public bool ContainsFocus(IUIElement scope, object target)
    {
        VerifyAccess();
        var root = ((UIElement)scope).Control;
        return target is Control control && GodotObject.IsInstanceValid(root) && GodotObject.IsInstanceValid(control)
            && root.IsInsideTree() && root.GetViewport() == _viewport && (root == control || root.IsAncestorOf(control));
    }
    public bool TryFocus(IUIElement? scope, object? target)
    {
        VerifyAccess();
        var root = scope == null ? RootMount : ((UIElement)scope).Control;
        bool Usable(Control c) => GodotObject.IsInstanceValid(c) && !c.IsQueuedForDeletion() && c.IsInsideTree()
            && c.GetViewport() == _viewport && c.IsVisibleInTree() && c.FocusMode != Control.FocusModeEnum.None
            && (c is not BaseButton button || !button.Disabled);
        Control? control;
        if (target != null)
        {
            if (target is not Control candidate || !Usable(candidate) || root != null &&
                (!GodotObject.IsInstanceValid(root) || root != candidate && !root.IsAncestorOf(candidate))) return false;
            control = candidate;
        }
        else
        {
            if (scope == null || !GodotObject.IsInstanceValid(root)) return false;
            control = UIElement.Enumerate(root!).FirstOrDefault(Usable);
            if (control == null) return false;
        }
        control.GrabFocus();
        return control.HasFocus();
    }
    public void ClearFocus()
    {
        VerifyAccess();
        var selected = _viewport.GuiGetFocusOwner();
        if (selected != null && (RootMount == null || GodotObject.IsInstanceValid(RootMount)
            && (selected == RootMount || RootMount.IsAncestorOf(selected)))) selected.ReleaseFocus();
    }
    public void Error(string message) => GD.PushError($"[UI] {message}");
    public void Exception(Exception exception, string context) => GD.PushError($"[UI] {context}\n{exception}");
}

public sealed class UIResourceLoader : IUIResourceLoader
{
    private readonly UIHost _host;
    public UIResourceLoader(UIHost host) => _host = host;
    public Task<IUIResource> LoadAsync(string resource, CancellationToken token)
    {
        _host.VerifyAccess(); token.ThrowIfCancellationRequested();
        var scene = ResourceLoader.Load<PackedScene>(resource)
            ?? throw new IOException($"Cannot load UI resource '{resource}'.");
        token.ThrowIfCancellationRequested();
        return Task.FromResult<IUIResource>(new SceneResource(_host, resource, scene));
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
