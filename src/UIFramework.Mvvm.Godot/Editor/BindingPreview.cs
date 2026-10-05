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
            foreach (var b in View.Bindings.Where(b => b.Enabled && b.Kind == ViewBindingKind.Property))
                node.Bind(b.Source, EngineMvvm.BindingTargets.Property(View.GetParent().GetNode<Control>(b.Target), b.Member), Converters.Configure(b.Converter, "", BindingMode.OneWay, b.ConverterInput, b.ConverterConstant, b.HasNullValue, b.NullValue));
        }
    }
    public BindingPreview(MvvmBindings view)
    {
        foreach (var source in view.Sources.Where(s => s.Creation == ViewModelCreation.Manual))
        { if (!PreviewSources.Has(source.Type)) throw new InvalidOperationException("缺少预览来源：" + source.Name); var type = source.Type; _runtime.RegisterFactory(type, () => PreviewSources.Create(type)); }
        _ui = EngineUI.UI.Create(view);
        _ui.ConfigureMvvm(_runtime, node => node.Name == "Preview" ? new Configuration { Sources = CloneSources(view.Sources, true) }
            : node.Element is EngineUI.UIElement display && display.Control.GetChildren().OfType<MvvmBindings>().SingleOrDefault() is { } binding ? new Configuration { Sources = CloneSources(binding.Sources, false), View = binding } : null, new EngineMvvm.ViewModelAssets());
        try
        {
            var node = new UINode(); var attached = node.AttachAsync(_ui.Root, "Preview"); if (!attached.IsCompleted) throw new InvalidOperationException("Preview initialization must be immediate."); attached.GetAwaiter().GetResult();
            foreach (var b in view.Bindings.Where(b => b.Enabled && b.Kind != ViewBindingKind.Command))
            {
                var control = view.GetParent().GetNode<Control>(b.Target);
                if (b.Kind == ViewBindingKind.Collection)
                { foreach (var child in control.GetChildren().OfType<Control>()) { _children.Add((child, child.Visible)); child.Hide(); } node.BindCollection(b.Source, new EngineMvvm.CollectionTarget(node, control, b.ItemScene!), b.OneTimeCollection); }
                else
                { var target = EngineMvvm.BindingTargets.Property(control, b.Member); _values.Add((target, target.Read())); node.Bind(b.Source, target, Converters.Configure(b.Converter, "", b.Mode == BindingMode.OneTime ? BindingMode.OneTime : BindingMode.OneWay, b.ConverterInput, b.ConverterConstant, b.HasNullValue, b.NullValue)); }
            }
        }
        catch { GodotPreviewTasks.Observe(DisposeAsync().AsTask()); throw; }
    }
    private static IReadOnlyList<ViewModelSource> CloneSources(IReadOnlyList<ViewModelSource> sources, bool root) => sources.Select(s => new ViewModelSource { Name = s.Name, Type = s.Type, Creation = !root && s.Creation == ViewModelCreation.Manual ? ViewModelCreation.Manual : ViewModelCreation.CreateInstance, Template = s.Template }).ToArray();
    public ValueTask DisposeAsync() { _dispose ??= Close(); return new ValueTask(_dispose); }
    private async Task Close()
    {
        Exception? first = null;
        try { await _ui.DisposeAsync(); } catch (Exception e) { first = e; }
        foreach (var value in _values) try { value.Target.Write(value.Value); } catch (Exception e) { Failure(ref first, e); }
        foreach (var child in _children) if (GodotObject.IsInstanceValid(child.Control)) child.Control.Visible = child.Visible;
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    private static void Failure(ref Exception? first, Exception error)
    { if (first == null) first = error; else { if (first.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> errors) first.Data[UIFailure.SecondaryExceptionsKey] = errors = new(); errors.Add(error); } }
}
internal static class GodotPreviewTasks
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
