using Godot;

namespace UIFramework.Godot;

/// <summary>Godot setup helpers. Must be called on the scene thread.</summary>
public static class GodotUI
{
    /// <summary>Create a context from a live scene node. Creates only the structural Root.</summary>
    public static UIContext Create(global::Godot.Node owner, IUIResourceLoader? loader = null, IUILog? logger = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (!GodotObject.IsInstanceValid(owner) || owner.IsQueuedForDeletion())
                throw new ObjectDisposedException(nameof(owner));
            if (!owner.IsInsideTree()) throw new InvalidOperationException("The UI owner must be inside the Godot scene tree.");
            var host = new GodotUIHost(owner.GetViewport());
            return new UIContext(host, loader ?? new GodotSceneLoader(host), logger ?? host);
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
}

/// <summary>Convenience APIs for borrowed scene Controls; no resources are loaded or cloned.</summary>
public static class GodotUIExtensions
{
    /// <summary>Attach an existing node to a borrowed Control and return the same node.</summary>
    public static async Task<T> AttachToAsync<T>(this T node, UINode parent, string name, Control control,
        CancellationToken token = default) where T : UINode
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);
            VerifyControl(parent, control);
            await node.AttachAsync(parent, name, new GodotUIElement(control), token);
            return node;
        }
        catch (Exception ex) { GodotUI.Report(ex, $"Attach Godot node '{name}'"); throw; }
    }

    /// <summary>Create and attach a container to a borrowed mount. No project containers are supplied automatically.</summary>
    public static async Task<T> CreateContainerAsync<T>(this UINode parent, string name, Control mount,
        CancellationToken token = default) where T : ContainerNode, new()
    {
        try
        {
            VerifyControl(parent, mount);
            token.ThrowIfCancellationRequested();
            return await new T().AttachToAsync(parent, name, mount, token);
        }
        catch (Exception ex) { GodotUI.Report(ex, $"Create Godot container '{name}'"); throw; }
    }

    /// <summary>Bind a borrowed embedded page using a basic node.</summary>
    public static Task BindAsync(this TabContainerNode tabs, string name, Control page,
        CancellationToken token = default) => tabs.BindAsync(name, new UINode(), page, token);

    /// <summary>Bind a borrowed embedded page using a custom node.</summary>
    public static async Task BindAsync(this TabContainerNode tabs, string name, UINode node, Control page,
        CancellationToken token = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);
            VerifyControl(tabs, page);
            await tabs.BindAsync(name, node, new GodotUIElement(page), token);
        }
        catch (Exception ex) { GodotUI.Report(ex, $"Bind Godot Tab '{name}'"); throw; }
    }

    /// <summary>Get the live Control associated with this logical node.</summary>
    public static Control GetControl(this UINode node)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);
            node.Context.Host.VerifyAccess();
            if (node.State is NodeState.Disposing or NodeState.Disposed) throw new ObjectDisposedException(node.Name);
            if (node.Element is not GodotUIElement display) throw new InvalidOperationException("Node does not have a Godot Control display.");
            VerifyControl(node, display.Control);
            return display.Control;
        }
        catch (Exception ex) { GodotUI.Report(ex, "Get Godot Control"); throw; }
    }

    /// <summary>Get a typed child Control; missing paths and mismatched types fail explicitly.</summary>
    public static T GetControl<T>(this UINode node, string path) where T : Control
    {
        try
        {
            return node.GetControl().GetNode<T>(path)
                ?? throw new KeyNotFoundException($"Godot Control path '{path}' was not found.");
        }
        catch (Exception ex) { GodotUI.Report(ex, $"Get Godot Control '{path}'"); throw; }
    }

    private static void VerifyControl(UINode parent, Control control)
    {
        ArgumentNullException.ThrowIfNull(parent);
        parent.Context.Host.VerifyAccess();
        ArgumentNullException.ThrowIfNull(control);
        if (!GodotObject.IsInstanceValid(control) || control.IsQueuedForDeletion())
            throw new ObjectDisposedException(nameof(control));
        if (!control.IsInsideTree()) throw new InvalidOperationException("The borrowed Control must be inside the Godot scene tree.");
        if (parent.Context.Host is not GodotUIHost) throw new InvalidOperationException("Godot helpers require a Godot UI context.");
    }
}
