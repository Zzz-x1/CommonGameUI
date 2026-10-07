#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreNode = global::UIFramework.UINode;
using UnityEngine;

namespace UIFramework.Unity
{
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public abstract class UINode : MonoBehaviour, IUINode
{
    private CoreNode? _node;
    private Action<Task>? _observe;
    public CoreNode Node
    {
        get
        {
            if (_node != null) return _node;
            _node = CreateNode();
            _node.AddLifecycle(new NativeLifecycle(this));
            return _node;
        }
    }
    protected virtual CoreNode CreateNode() => new NativeState(this);
    public Guid Id => Node.Id;
    public string UIName => Node.Name;
    string IUINode.Name => Node.Name;
    public UIContext Context => Node.Context;
    public IUINode? Parent => Node.Parent?.Owner;
    public IReadOnlyList<IUINode> Children => Node.Children;
    public IUIElement? Element => Node.Element;
    public NodeState State => Node.State;
    public bool HasEntered => Node.HasEntered;
    public bool IsCached => Node.IsCached;
    public bool IsUIVisible => Node.IsVisible;
    bool IUINode.IsVisible => Node.IsVisible;
    public bool IsActive => Node.IsActive;
    public bool IsInteractive => Node.IsInteractive;
    public CancellationToken Lifetime => Node.Lifetime;
    public int TransitionOrder { get => Node.TransitionOrder; set => Node.TransitionOrder = value; }
    public IUITransition? Transition { get => Node.Transition; set => Node.Transition = value; }
    public TransitionState? TransitionState => Node.TransitionState;
    public bool IsTransitioning => Node.IsTransitioning;
    public TransitionPhase? CurrentTransitionPhase => Node.CurrentTransitionPhase;
    public float TransitionProgress => Node.TransitionProgress;
    public Func<CancellationToken, Task>? DismissRequested { get => Node.DismissRequested; set => Node.DismissRequested = value; }
    public event Action<TransitionState>? TransitionChanged { add => Node.TransitionChanged += value; remove => Node.TransitionChanged -= value; }
    public IUINode GetChild(string name) => Node.GetChild(name);
    public void AddLifecycle(INodeLifecycle handler) => Node.AddLifecycle(handler);
    public void RemoveLifecycle(INodeLifecycle handler) => Node.RemoveLifecycle(handler);
    public ValueTask DisposeAsync() => Node.DisposeAsync();
    protected virtual bool InitiallyVisible => false;
    protected virtual Task OnInitializeAsync(CancellationToken token) => Task.CompletedTask;
    protected virtual Task OnEnteringAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    protected virtual void OnEntered(LifecycleContext context) { }
    protected virtual Task OnExitingAsync(LifecycleContext context, CancellationToken token) => Task.CompletedTask;
    protected virtual void OnExited(LifecycleContext context) { }
    protected virtual void OnActivated(StateChangeContext context) { }
    protected virtual void OnDeactivated(StateChangeContext context) { }
    protected virtual void OnVisibilityChanged(StateChangeContext context) { }
    protected virtual Task OnDisposeAsync() => Task.CompletedTask;
    protected virtual void OnApplyState() { }
    protected virtual void OnChildRemoved(IUINode child) { }
    protected virtual Task OnChildRemovedAsync(IUINode child) => Task.CompletedTask;
    protected virtual void OnSetParameters(object? args)
    {
        if (args != null) throw new ArgumentException($"{GetType().Name} does not accept parameters.", nameof(args));
    }
    protected void Observe(Task task) => (_observe ?? throw new InvalidOperationException("UI initialization has not prepared its task observer."))(task);
    private void PrepareLifetime()
    {
        var runner = LifetimeRunner.Get();
        _observe = runner.Observe;
        runner.BindIntrinsic(Node, this);
    }
    private sealed class NativeState : CoreNode
    {
        private readonly UINode _owner;
        public NativeState(UINode owner) : base(owner) { _owner = owner; }
        protected override bool InitiallyVisible => _owner.InitiallyVisible;
        protected override void OnSetParameters(object? args) => _owner.OnSetParameters(args);
        protected override void OnApplyState() => _owner.OnApplyState();
        protected override void OnChildRemoved(CoreNode child) => _owner.OnChildRemoved(child.Owner);
        protected override Task OnChildRemovedAsync(CoreNode child) => _owner.OnChildRemovedAsync(child.Owner);
    }
    private sealed class NativeLifecycle : NodeLifecycle
    {
        private readonly UINode _owner;
        public NativeLifecycle(UINode owner) { _owner = owner; }
        public override Task InitializeAsync(IUINode node, CancellationToken token)
        { _owner.PrepareLifetime(); return _owner.OnInitializeAsync(token); }
        public override Task EnteringAsync(LifecycleContext context, CancellationToken token) => _owner.OnEnteringAsync(context, token);
        public override void Entered(LifecycleContext context) => _owner.OnEntered(context);
        public override Task ExitingAsync(LifecycleContext context, CancellationToken token) => _owner.OnExitingAsync(context, token);
        public override void Exited(LifecycleContext context) => _owner.OnExited(context);
        public override void Activated(StateChangeContext context) => _owner.OnActivated(context);
        public override void Deactivated(StateChangeContext context) => _owner.OnDeactivated(context);
        public override void VisibilityChanged(StateChangeContext context) => _owner.OnVisibilityChanged(context);
        public override Task DisposeAsync(IUINode node) => _owner.OnDisposeAsync();
    }
}

public abstract class UINode<TArgs> : UINode, IParameterizedNode<TArgs>
{
    public TArgs Parameters { get; private set; } = default!;
    protected override void OnSetParameters(object? args)
    {
        if (!(args is TArgs typed)) throw new ArgumentException($"Expected parameters of type {typeof(TArgs).FullName}.", nameof(args));
        Parameters = typed;
    }
}

public abstract class DialogNode<TResult> : UINode, IDialogNode<TResult>
{
    protected override CoreNode CreateNode() => new NativeDialogState(this);
    public void Complete(TResult result) => ((global::UIFramework.DialogNode<TResult>)Node).Complete(result);
    public Task<TResult> WaitForResultAsync(CancellationToken token) => ((global::UIFramework.DialogNode<TResult>)Node).WaitForResultAsync(token);
    private sealed class NativeDialogState : global::UIFramework.DialogNode<TResult>
    {
        private readonly DialogNode<TResult> _owner;
        public NativeDialogState(DialogNode<TResult> owner) : base(owner) { _owner = owner; }
        protected override bool InitiallyVisible => _owner.InitiallyVisible;
        protected override void OnSetParameters(object? args) => _owner.OnSetParameters(args);
        protected override void OnApplyState() => _owner.OnApplyState();
        protected override void OnChildRemoved(CoreNode child) => _owner.OnChildRemoved(child.Owner);
        protected override Task OnChildRemovedAsync(CoreNode child) => _owner.OnChildRemovedAsync(child.Owner);
    }
}
}
