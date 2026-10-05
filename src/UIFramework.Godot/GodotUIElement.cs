using Godot;
using System.Runtime.CompilerServices;

namespace UIFramework.Godot;

/// <summary>Owned scene root or borrowed embedded Control. All access belongs to the scene thread.</summary>
public sealed class GodotUIElement : IUIElement
{
    private sealed record ControlState(Control Control, Control.MouseFilterEnum Mouse, Control.FocusModeEnum Focus);
    private static readonly ConditionalWeakTable<Control, ControlState> Defaults = new();
    private readonly bool _owned;
    private bool _disposed;
    private bool _lifetimeEnding;
    private readonly GodotUIElement? _lifetimeOwner;
    internal bool IsLifetimeEnding => _lifetimeEnding || (_lifetimeOwner?.IsLifetimeEnding ?? false);
    internal void BeginLifetimeDisposal() => _lifetimeEnding = true;
    internal bool DisplayWasDestroyed => !_disposed && IsLifetimeEnding && (!GodotObject.IsInstanceValid(Control) || Control.IsQueuedForDeletion());
    private readonly string _name;

    public GodotUIElement(Control control, bool owned = false) : this(control, owned, null) { }
    internal GodotUIElement(Control control, bool owned, GodotUIElement? lifetimeOwner)
    {
        _lifetimeOwner = lifetimeOwner;
        Control = control ?? throw new ArgumentNullException(nameof(control));
        _name = control.Name;
        _owned = owned;
        foreach (var child in Enumerate(control))
            Defaults.GetValue(child, c => new ControlState(c, c.MouseFilter, c.FocusMode));
    }

    public Control Control { get; }
    public T Get<T>(string path) where T : global::Godot.Node => Control.GetNode<T>(path);

    private void VerifyAlive()
    {
        if (_disposed || !GodotObject.IsInstanceValid(Control) || Control.IsQueuedForDeletion())
            throw new ObjectDisposedException(nameof(GodotUIElement), $"Display '{_name}' is no longer available.");
    }

    public void SetVisible(bool visible) { if (DisplayWasDestroyed) return; VerifyAlive(); Control.Visible = visible; }
    public void SetInteractive(bool interactive)
    {
        if (DisplayWasDestroyed) return;
        VerifyAlive();
        foreach (var control in Enumerate(Control))
        {
            var state = Defaults.GetValue(control, c => new(c, c.MouseFilter, c.FocusMode));
            state.Control.MouseFilter = interactive ? state.Mouse : Control.MouseFilterEnum.Ignore;
            state.Control.FocusMode = interactive ? state.Focus : Control.FocusModeEnum.None;
            // Mouse/focus gating must not overwrite a business-controlled Button.Disabled value.
        }
    }
    public void BringToFront() { if (DisplayWasDestroyed) return; VerifyAlive(); Control.MoveToFront(); }
    public object CaptureOrder() { VerifyAlive(); return Control.GetIndex(); }
    public void RestoreOrder(object order)
    {
        if (DisplayWasDestroyed) return;
        VerifyAlive();
        if (order is not int index) throw new ArgumentException("Expected a Godot sibling index.", nameof(order));
        Control.GetParent().MoveChild(Control, index);
    }
    public ValueTask DisposeAsync()
    {
        if (_disposed) return default;
        if (DisplayWasDestroyed) { _disposed = true; return default; }
        if (!GodotObject.IsInstanceValid(Control) || Control.IsQueuedForDeletion())
            throw new ObjectDisposedException(nameof(GodotUIElement), "Display was destroyed before its logical owner.");
        if (_owned)
        {
            // Remove the disposed display from sibling ordering immediately. QueueFree alone
            // leaves a stale child until frame end while the next transition captures indices.
            Control.GetParent().RemoveChild(Control);
            Control.QueueFree();
        }
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
