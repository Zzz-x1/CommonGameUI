using CoreNode = global::UIFramework.UINode;
using Godot;

namespace UIFramework.Godot;

/// <summary>Godot setup helpers. Must be called on the scene thread.</summary>
public static class UI
{
    /// <summary>Create a context from a live scene node. Creates only the structural Root.</summary>
    public static UIContext Create(global::Godot.Node owner, IUIResourceLoader? loader = null, IUILog? logger = null, Control? rootMount = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (!GodotObject.IsInstanceValid(owner) || owner.IsQueuedForDeletion())
                throw new ObjectDisposedException(nameof(owner));
            if (!owner.IsInsideTree()) throw new InvalidOperationException("The UI owner must be inside the Godot scene tree.");
            var host = new UIHost(owner.GetViewport(), rootMount ?? owner as Control);
            return new UIContext(host, loader ?? new UIResourceLoader(host), logger ?? host);
        }
        catch (Exception ex) { Report(ex, "Create Godot UI context"); throw; }
    }

    // Use the same marker/protocol as the core without adding an engine dependency to it.
    internal static void Report(Exception exception, string operation)
    {
        const string logged = "UIFramework.Logged";
        if (exception.Data.Contains(logged)) return;
        exception.Data[logged] = true;
        try { GD.PushError($"[UI] {operation}\n{exception}"); }
        catch (Exception loggingFailure)
        {
            if (exception.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> failures)
                exception.Data[UIFailure.SecondaryExceptionsKey] = failures = new List<Exception>();
            failures.Add(loggingFailure);
            Console.Error.WriteLine(loggingFailure);
        }
    }

    internal static T Configure<T>(Func<T> create)
    {
        try { return create(); }
        catch (Exception ex) { Report(ex, "Configure Godot UI effect"); throw; }
    }
    internal static void Cleanup(Exception? primary, Action cleanup)
    {
        try { cleanup(); }
        catch (Exception ex)
        {
            Report(ex, "Restore Godot UI effect");
            if (primary == null) throw;
            if (primary.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> failures)
                primary.Data[UIFailure.SecondaryExceptionsKey] = failures = new List<Exception>();
            failures.Add(ex);
        }
    }
}

/// <summary>Convenience APIs for borrowed scene Controls; no resources are loaded or cloned.</summary>
public static class UIExtensions
{
        public static Task AttachToAsync(this UINode node, IUINode parent, string name, CancellationToken token = default) =>
            node.Node.AttachAsync(parent, name, new UIElement(node), token);
        public static Task BindAsync(this TabContainerNode tabs, string name, UINode node, CancellationToken token = default) =>
            tabs.BindAsync(name, node, new UIElement(node), token);

    /// <summary>Attach an existing node to a borrowed Control. Returns the core initialization task directly.</summary>
    public static Task AttachToAsync(this IUINode node, IUINode parent, string name, Control control,
        CancellationToken token = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);
            VerifyControl(parent, control);
            return node.Node.AttachAsync(parent, name, new UIElement(control), token);
        }
        catch (Exception ex) { UI.Report(ex, $"Attach Godot node '{name}'"); throw; }
    }

    /// <summary>Create a container with an owned full-size mount, or borrow an explicit mount.</summary>
    public static async Task<T> CreateContainerAsync<T>(this IUINode parent, string name, Control? mount = null,
        CancellationToken token = default) where T : ContainerNode, new()
    {
        Control? owned = null;
        try
        {
            parent.Context.Host.VerifyAccess();
            token.ThrowIfCancellationRequested();
            var node = new T();
            var automatic = mount == null;
            if (automatic)
            {
                var host = parent.Context.Host as UIHost ?? throw new InvalidOperationException("Expected a Godot UI context.");
                var ancestor = host.RootMount;
                for (var current = parent; current != null; current = current.Parent)
                {
                    if (current.Element == null) continue;
                    ancestor = (current.Element as UIElement ?? throw new InvalidOperationException("Expected a Godot display.")).Control;
                    break;
                }
                if (ancestor == null) throw new InvalidOperationException("Automatic container mounting requires a parent display or a UIHost root mount.");
                VerifyControl(parent, ancestor);
                owned = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
                ancestor.AddChild(owned);
                owned.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                mount = owned;
            }
            VerifyControl(parent, mount!);
            await node.AttachAsync(parent, name, new UIElement(mount!, owned: automatic), token);
            return node;
        }
        catch (Exception ex)
        {
            if (owned != null && GodotObject.IsInstanceValid(owned) && !owned.IsQueuedForDeletion())
                UI.Cleanup(ex, () => { owned.GetParent()?.RemoveChild(owned); owned.QueueFree(); });
            UI.Report(ex, $"Create Godot container '{name}'");
            throw;
        }
    }

    /// <summary>Bind a borrowed embedded page using a basic node.</summary>
    public static Task BindAsync(this TabContainerNode tabs, string name, Control page,
        CancellationToken token = default) => tabs.BindAsync(name, new CoreNode(), page, token);

    /// <summary>Bind a borrowed embedded page using a custom node.</summary>
    public static Task BindAsync(this TabContainerNode tabs, string name, IUINode node, Control page,
        CancellationToken token = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);
            VerifyControl(tabs, page);
            return tabs.BindAsync(name, node, new UIElement(page), token);
        }
        catch (Exception ex) { UI.Report(ex, $"Bind Godot Tab '{name}'"); throw; }
    }

    /// <summary>Get the live Control associated with this logical node.</summary>
    public static Control GetRoot(this IUINode node)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);
            node.Context.Host.VerifyAccess();
            if (node.State is NodeState.Disposing or NodeState.Disposed) throw new ObjectDisposedException(node.Name);
            if (node.Element is not UIElement display) throw new InvalidOperationException("Node does not have a Godot Control display.");
            VerifyControl(node, display.Control);
            return display.Control;
        }
        catch (Exception ex) { UI.Report(ex, "Get Godot Control"); throw; }
    }

    /// <summary>Get a typed child Control; missing paths and mismatched types fail explicitly.</summary>
    public static T GetControl<T>(this IUINode node, string path) where T : Control
    {
        try
        {
            return node.GetRoot().GetNode<T>(path)
                ?? throw new KeyNotFoundException($"Godot Control path '{path}' was not found.");
        }
        catch (Exception ex) { UI.Report(ex, $"Get Godot Control '{path}'"); throw; }
    }

    private static void VerifyControl(IUINode parent, Control control)
    {
        ArgumentNullException.ThrowIfNull(parent);
        parent.Context.Host.VerifyAccess();
        ArgumentNullException.ThrowIfNull(control);
        if (!GodotObject.IsInstanceValid(control) || control.IsQueuedForDeletion())
            throw new ObjectDisposedException(nameof(control));
        if (!control.IsInsideTree()) throw new InvalidOperationException("The borrowed Control must be inside the Godot scene tree.");
        if (parent.Context.Host is not UIHost) throw new InvalidOperationException("Godot helpers require a Godot UI context.");
    }
}
