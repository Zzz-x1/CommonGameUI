namespace UIFramework;

public enum LifecycleReason { Initialization, Open, Show, Hide, Push, Covered, Switch, Return, Close, Modal, Ancestor, Disposal }

/// <summary>Immutable description of a lifecycle operation. Source/Target may be absent.</summary>
public sealed class LifecycleContext
{
    public LifecycleContext(UINode node, ContainerNode? container, LifecycleReason reason, UINode? source = null, UINode? target = null)
    { Node = node; Container = container; Reason = reason; Source = source; Target = target; IsFirstEnter = !node.HasEntered; }
    public UINode Node { get; }
    public ContainerNode? Container { get; }
    public LifecycleReason Reason { get; }
    public UINode? Source { get; }
    public UINode? Target { get; }
    public bool IsFirstEnter { get; }
    internal LifecycleContext For(UINode node) => node == Node ? this : new(node, Container, node == Node ? Reason : Container is ModalContainerNode ? LifecycleReason.Modal : LifecycleReason.Ancestor, Source, Target);
}

public sealed class StateChangeContext
{
    public StateChangeContext(LifecycleContext operation, bool previous, bool current)
    { Operation = operation; Previous = previous; Current = current; }
    public LifecycleContext Operation { get; }
    public UINode Node => Operation.Node;
    public bool Previous { get; }
    public bool Current { get; }
}

public interface INodeLifecycle
{
    Task InitializeAsync(UINode node, CancellationToken token);
    Task EnteringAsync(LifecycleContext context, CancellationToken token);
    void Entered(LifecycleContext context);
    Task ExitingAsync(LifecycleContext context, CancellationToken token);
    void Exited(LifecycleContext context);
    void Activated(StateChangeContext context);
    void Deactivated(StateChangeContext context);
    void VisibilityChanged(StateChangeContext context);
    Task DisposeAsync(UINode node);
}

/// <summary>Override only the stages your reusable behavior needs.</summary>
public abstract class NodeLifecycle : INodeLifecycle
{
    public virtual Task InitializeAsync(UINode node, CancellationToken token) => Task.CompletedTask;
    public virtual Task EnteringAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    public virtual void Entered(LifecycleContext context) { }
    public virtual Task ExitingAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    public virtual void Exited(LifecycleContext context) { }
    public virtual void Activated(StateChangeContext context) { }
    public virtual void Deactivated(StateChangeContext context) { }
    public virtual void VisibilityChanged(StateChangeContext context) { }
    public virtual Task DisposeAsync(UINode node) => Task.CompletedTask;
}
