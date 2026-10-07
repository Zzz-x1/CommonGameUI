namespace UIFramework;

/// <summary>A business node identity, backed by the renderer-independent state machine.</summary>
public interface IUINode : IAsyncDisposable
{
    UINode Node { get; }
    Guid Id { get; }
    string Name { get; }
    UIContext Context { get; }
    IUINode? Parent { get; }
    IReadOnlyList<IUINode> Children { get; }
    IUIElement? Element { get; }
    NodeState State { get; }
    bool HasEntered { get; }
    bool IsCached { get; }
    bool IsVisible { get; }
    bool IsActive { get; }
    bool IsInteractive { get; }
    CancellationToken Lifetime { get; }
    int TransitionOrder { get; set; }
    IUITransition? Transition { get; set; }
    TransitionState? TransitionState { get; }
    bool IsTransitioning { get; }
    TransitionPhase? CurrentTransitionPhase { get; }
    float TransitionProgress { get; }
    Func<CancellationToken, Task>? DismissRequested { get; set; }
    event Action<TransitionState>? TransitionChanged;
    IUINode GetChild(string name);
    void AddLifecycle(INodeLifecycle handler);
    void RemoveLifecycle(INodeLifecycle handler);
}

/// <summary>Implemented by native displays that contain their business script.</summary>
public interface IUIResourceNodeProvider
{
    IUINode GetNode(Type businessType);
}

public interface IParameterizedNode<out TArgs> : IUINode
{
    TArgs Parameters { get; }
}

public interface IDialogNode<TResult> : IUINode
{
    void Complete(TResult result);
    Task<TResult> WaitForResultAsync(CancellationToken token);
}
