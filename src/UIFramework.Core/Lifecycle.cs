namespace UIFramework;

public enum LifecycleReason { Initialization, Open, Show, Hide, Push, Covered, Switch, Return, Close, Modal, Ancestor, Disposal }

/// <summary>Immutable description of a lifecycle operation. Source/Target may be absent.</summary>
public sealed class LifecycleContext
{
    public LifecycleContext(IUINode node, ContainerNode? container, LifecycleReason reason, IUINode? source = null, IUINode? target = null)
        : this(node, container, reason, source, target, node.Node.CacheReuse) { }
    private LifecycleContext(IUINode node, ContainerNode? container, LifecycleReason reason, IUINode? source, IUINode? target, bool isCacheReuse)
    { Node = node.Node.Owner; Container = container; Reason = reason; Source = source?.Node.Owner; Target = target?.Node.Owner; IsFirstEnter = !node.HasEntered; IsCacheReuse = isCacheReuse; }
    public IUINode Node { get; }
    public ContainerNode? Container { get; }
    public LifecycleReason Reason { get; }
    public IUINode? Source { get; }
    public IUINode? Target { get; }
    public bool IsFirstEnter { get; }
    public bool IsCacheReuse { get; }
    internal LifecycleContext For(UINode node) => ReferenceEquals(node.Owner, Node) ? this : new(node, Container, ReferenceEquals(node.Owner, Node) ? Reason : Container is ModalContainerNode ? LifecycleReason.Modal : LifecycleReason.Ancestor, Source, Target, IsCacheReuse);
}

public sealed class StateChangeContext
{
    public StateChangeContext(LifecycleContext operation, bool previous, bool current)
    { Operation = operation; Previous = previous; Current = current; }
    public LifecycleContext Operation { get; }
    public IUINode Node => Operation.Node;
    public bool Previous { get; }
    public bool Current { get; }
}

public interface INodeLifecycle
{
    Task InitializeAsync(IUINode node, CancellationToken token);
    Task EnteringAsync(LifecycleContext context, CancellationToken token);
    void Entered(LifecycleContext context);
    Task ExitingAsync(LifecycleContext context, CancellationToken token);
    void Exited(LifecycleContext context);
    void Activated(StateChangeContext context);
    void Deactivated(StateChangeContext context);
    void VisibilityChanged(StateChangeContext context);
    Task DisposeAsync(IUINode node);
}

/// <summary>Override only the stages your reusable behavior needs.</summary>
public abstract class NodeLifecycle : INodeLifecycle
{
    public virtual Task InitializeAsync(IUINode node, CancellationToken token) => Task.CompletedTask;
    public virtual Task EnteringAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    public virtual void Entered(LifecycleContext context) { }
    public virtual Task ExitingAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    public virtual void Exited(LifecycleContext context) { }
    public virtual void Activated(StateChangeContext context) { }
    public virtual void Deactivated(StateChangeContext context) { }
    public virtual void VisibilityChanged(StateChangeContext context) { }
    public virtual Task DisposeAsync(IUINode node) => Task.CompletedTask;
}
