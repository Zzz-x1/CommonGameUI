using System.Runtime.CompilerServices;
using Godot;

namespace UIFramework.Mvvm.Godot;

[Tool]
internal sealed partial class MvvmObserver : global::Godot.Node
{
    private static readonly ConditionalWeakTable<SceneTree, MvvmObserver> Observers = new();
    private readonly MvvmFrameDriver _driver = new();
    private SceneTree _tree = null!;

    internal static void Attach(SceneTree tree, MvvmRuntime runtime)
    {
        if (!Observers.TryGetValue(tree, out var observer))
        {
            observer = new MvvmObserver
            {
                _tree = tree,
                ProcessMode = ProcessModeEnum.Always,
                ProcessThreadGroup = ProcessThreadGroupEnum.MainThread,
                ProcessThreadGroupOrder = int.MaxValue,
                ProcessPriority = int.MaxValue
            };
            Observers.Add(tree, observer);
            // A context may be created while the scene root is adding its children.
            tree.Root.CallDeferred(MethodName.AddChild, observer, false, (int)InternalMode.Back);
        }
        observer._driver.Attach(runtime);
    }

    public override void _Process(double delta)
    {
        try { _driver.Tick(); }
        finally
        {
            if (_driver.IsEmpty)
            {
                Observers.Remove(_tree);
                QueueFree();
            }
        }
    }

    public override void _ExitTree()
    {
        if (Observers.TryGetValue(_tree, out var current) && ReferenceEquals(current, this))
            Observers.Remove(_tree);
    }
}
