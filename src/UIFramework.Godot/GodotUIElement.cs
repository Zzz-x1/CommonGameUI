using Godot;
using System.Runtime.CompilerServices;

namespace UIFramework.Godot;

/// <summary>Owned scene root or borrowed embedded Control. All access belongs to the scene thread.</summary>
public sealed class GodotUIElement : IUIElement
{
    private sealed record ControlState(Control Control, Control.MouseFilterEnum Mouse, Control.FocusModeEnum Focus);
    private static readonly ConditionalWeakTable<Control, ControlState> Defaults = new();
    private readonly List<ControlState> _controls = new();
    private readonly bool _owned;
    private bool _disposed;
    private readonly string _name;

    public GodotUIElement(Control control, bool owned = false)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        _name = control.Name;
        _owned = owned;
        foreach (var child in Enumerate(control))
            _controls.Add(Defaults.GetValue(child, c => new ControlState(c, c.MouseFilter, c.FocusMode)));
    }

    public Control Control { get; }
    public T Get<T>(string path) where T : global::Godot.Node => Control.GetNode<T>(path);

    private void VerifyAlive()
    {
        if (_disposed || !GodotObject.IsInstanceValid(Control) || Control.IsQueuedForDeletion())
            throw new ObjectDisposedException(nameof(GodotUIElement), $"Display '{_name}' is no longer available.");
    }

    public void SetVisible(bool visible) { VerifyAlive(); Control.Visible = visible; }
    public void SetInteractive(bool interactive)
    {
        VerifyAlive();
        foreach (var state in _controls)
        {
            if (!GodotObject.IsInstanceValid(state.Control) || state.Control.IsQueuedForDeletion())
                throw new ObjectDisposedException("An embedded control was destroyed outside its UI owner.");
            state.Control.MouseFilter = interactive ? state.Mouse : Control.MouseFilterEnum.Ignore;
            state.Control.FocusMode = interactive ? state.Focus : Control.FocusModeEnum.None;
            // Mouse/focus gating must not overwrite a business-controlled Button.Disabled value.
        }
    }
    public void BringToFront() { VerifyAlive(); Control.MoveToFront(); }
    public ValueTask DisposeAsync()
    {
        if (_disposed) return default;
        if (!GodotObject.IsInstanceValid(Control) || Control.IsQueuedForDeletion())
            throw new ObjectDisposedException(nameof(GodotUIElement), "Display was destroyed before its logical owner.");
        if (_owned) Control.QueueFree();
        _disposed = true;
        return default;
    }

    internal static IEnumerable<Control> Enumerate(global::Godot.Node root)
    {
        if (root is Control control) yield return control;
        foreach (var child in root.GetChildren())
            foreach (var nested in Enumerate(child)) yield return nested;
    }
}
