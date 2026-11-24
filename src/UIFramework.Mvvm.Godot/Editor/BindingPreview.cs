using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework.Godot;
namespace UIFramework.Mvvm.Godot;

public sealed class BindingPreview : IAsyncDisposable
{
    private readonly UIContext _ui;
    private readonly MvvmRuntime _runtime = new();
    private readonly List<(IBindingTarget Target, object? Value)> _values = new();
    private readonly List<(Control Control, bool Visible)> _children = new();
    private Task? _dispose;
    private sealed class Configuration : IViewBindingConfiguration
    {
        public IReadOnlyList<ViewModelSource> Sources { get; set; } = Array.Empty<ViewModelSource>();
        public MvvmBindings? View { get; set; }
        public void Apply(UINode node)
        {
            if (View == null) return;
            ViewBindingPlans.Apply(node, View, preview: true);
        }
    }
    public BindingPreview(MvvmBindings view)
    {
        _ui = EngineUI.UI.Create(view);
        _ui.ConfigureMvvm(_runtime, node => node.Name == "Preview" ? new Configuration { Sources = CloneSources(view.Sources, true) }
            : node.Element is EngineUI.UIElement display && display.Control.GetChildren().OfType<MvvmBindings>().SingleOrDefault() is { } binding ? new Configuration { Sources = CloneSources(binding.Sources, false), View = binding } : null, new EngineMvvm.ViewModelAssets());
        try
        {
            var node = new UINode(); var attached = node.AttachAsync(_ui.Root, "Preview"); if (!attached.IsCompleted) throw new InvalidOperationException("Preview initialization must be immediate."); attached.GetAwaiter().GetResult();
            ViewBindingPlans.Apply(node, view, preview: true,
                captureProperty: target => _values.Add((target, target.Read())),
                captureCollection: target =>
                {
                    foreach (var child in ((Control)target).GetChildren().OfType<Control>())
                    {
                        _children.Add((child, child.Visible));
                        child.Hide();
                    }
                });
        }
        catch { EditorMvvmTasks.Observe(DisposeAsync().AsTask()); throw; }
    }
    private static IReadOnlyList<ViewModelSource> CloneSources(IReadOnlyList<ViewModelSource> sources, bool root) => sources.Select(s => new ViewModelSource { Name = s.Name, Type = s.Type, Creation = !root && s.Creation == ViewModelCreation.Manual ? ViewModelCreation.Manual : ViewModelCreation.CreateInstance, Template = s.Template }).ToArray();
    public ValueTask DisposeAsync() { _dispose ??= Close(); return new ValueTask(_dispose); }
    private async Task Close()
    {
        Exception? first = null;
        try { await _ui.DisposeAsync(); } catch (Exception e) { first = e; }
        foreach (var value in _values.AsEnumerable().Reverse()) try { value.Target.Write(value.Value); } catch (Exception e) { Failure(ref first, e); }
        foreach (var child in _children.AsEnumerable().Reverse()) if (GodotObject.IsInstanceValid(child.Control)) child.Control.Visible = child.Visible;
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    private static void Failure(ref Exception? first, Exception error)
    { if (first == null) first = error; else { if (first.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> errors) first.Data[UIFailure.SecondaryExceptionsKey] = errors = new(); errors.Add(error); } }
}
internal static class EditorMvvmTasks
{
    private static readonly List<Task> Tasks = new();
    private static bool _attached;
    internal static void Observe(Task task)
    {
        Tasks.Add(task);
        if (_attached) return; _attached = true;
        ((SceneTree)Engine.GetMainLoop()).ProcessFrame += () => { foreach (var pending in Tasks.ToArray()) if (pending.IsCompleted) { Tasks.Remove(pending); pending.GetAwaiter().GetResult(); } };
    }
}
