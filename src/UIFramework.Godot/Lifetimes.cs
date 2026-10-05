using CoreNode = global::UIFramework.UINode;
using Godot;
using System.Runtime.CompilerServices;

namespace UIFramework.Godot;

/// <summary>Disconnecting a binding does not dispose its node or its engine owner.</summary>
public sealed class LifetimeBinding : IDisposable
{
    private readonly TaskCompletionSource<Task> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly LifetimeRunner _runner;
    private bool _connected = true;
    internal bool IsConnected => _connected;
    internal bool Intrinsic { get; }
    internal CoreNode Node { get; }
    internal global::Godot.Node Owner { get; }
    public Task Completion { get; }
    internal LifetimeBinding(LifetimeRunner runner, CoreNode node, global::Godot.Node owner, bool intrinsic)
    { Intrinsic = intrinsic; _runner = runner; Node = node; Owner = owner; Completion = _completion.Task.Unwrap(); }
    internal void End()
    {
        Disconnect();
        Task cleanup;
        try
        {
            if (!GodotObject.IsInstanceValid(Owner) || Owner.IsQueuedForDeletion()) Prepare(Node);
            cleanup = ReferenceEquals(Node, Node.Context.Root) ? Node.Context.DisposeAsync().AsTask() : Node.DisposeAsync().AsTask();
        }
        catch (Exception ex) { cleanup = Task.FromException(ex); }
        _completion.SetResult(cleanup);
        _runner.Observe(Completion);
    }
    private static void Prepare(CoreNode node)
    {
        if (node.Element is UIElement display) display.BeginLifetimeDisposal();
        foreach (var child in node.Children) Prepare(child.Node);
    }
    private void Disconnect() { _connected = false; _runner.Remove(this); }
    public void Dispose()
    {
        Node.Context.Host.VerifyAccess();
        if (!_connected) return;
        Disconnect(); _completion.SetResult(Task.CompletedTask);
    }
}

public static class LifetimeExtensions
{
    /// <summary>Pass a scene root to bind the whole scene's lifetime.</summary>
    public static LifetimeBinding BindLifetime(this UIContext ui, global::Godot.Node owner) => ui.Root.BindLifetime(owner);
    public static LifetimeBinding BindLifetime(this IUINode node, global::Godot.Node owner)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(owner);
            if (node.Context.Host is not UIHost) throw new InvalidOperationException("Expected a Godot UI context.");
            node.Context.Host.VerifyAccess();
            if (node.State is not (NodeState.Ready or NodeState.Initializing)) throw new InvalidOperationException("Bind an attached, live node.");
            if (!GodotObject.IsInstanceValid(owner) || owner.IsQueuedForDeletion()) throw new ObjectDisposedException(nameof(owner));
            if (!owner.IsInsideTree()) throw new InvalidOperationException("The owner must be inside the scene tree.");
            return LifetimeRunner.Get(owner.GetTree()).Bind(node.Node, owner);
        }
        catch (Exception ex) { UI.Report(ex, "Bind Godot lifetime"); throw; }
    }
}

// Observes frames on SceneTree rather than a scene object that can be freed.
internal sealed class LifetimeRunner
{
    private static readonly ConditionalWeakTable<SceneTree, LifetimeRunner> Runners = new();
    private readonly List<LifetimeBinding> _bindings = new();
    private readonly List<Task> _pending = new();
    private readonly List<TaskCompletionSource<bool>> _frames = new();
    private LifetimeRunner(SceneTree tree) => tree.ProcessFrame += Tick;
    internal static LifetimeRunner Get(SceneTree tree) => Runners.GetValue(tree, value => new LifetimeRunner(value));
    internal Task NextFrameAsync()
    {
        var completion = new TaskCompletionSource<bool>(); _frames.Add(completion); return completion.Task;
    }
    internal LifetimeBinding Bind(CoreNode node, global::Godot.Node owner, bool intrinsic = false)
    {
        if (_bindings.Any(b => ReferenceEquals(b.Node, node) && b.Intrinsic == intrinsic)) throw new InvalidOperationException("Node already has a lifetime binding.");
        var binding = new LifetimeBinding(this, node, owner, intrinsic); _bindings.Add(binding); return binding;
    }
    internal void BindIntrinsic(CoreNode node, global::Godot.Node owner) => Bind(node, owner, true);
    internal void Remove(LifetimeBinding binding) => _bindings.Remove(binding);
    internal void Observe(Task task) => _pending.Add(task);
    private void Tick()
    {
        var frames = _frames.ToArray(); _frames.Clear();
        foreach (var binding in _bindings.ToArray())
            if (binding.IsConnected && (!GodotObject.IsInstanceValid(binding.Owner) || binding.Owner.IsQueuedForDeletion() || binding.Node.State is NodeState.Disposing or NodeState.Disposed)) binding.End();
        foreach (var frame in frames) frame.SetResult(true);
        foreach (var task in _pending.ToArray())
        {
            if (!task.IsCompleted) continue;
            _pending.Remove(task);
            try { task.GetAwaiter().GetResult(); }
            catch (Exception ex) { UI.Report(ex, "Bound Godot lifetime cleanup"); throw; }
        }
    }
}
