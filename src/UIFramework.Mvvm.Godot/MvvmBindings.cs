using Godot;
using UIFramework.Godot;

namespace UIFramework.Mvvm.Godot;
[Tool, GlobalClass]
public partial class MvvmBindings : global::Godot.Node, IViewBindingConfiguration
{
    [Export] public global::Godot.Collections.Array<ViewModelSourceResource> ViewModels { get; set; } = new();
    [Export] public global::Godot.Collections.Array<ViewBindingResource> Bindings { get; set; } = new();
    public IReadOnlyList<ViewModelSource> Sources => ViewModels.Select(s => new ViewModelSource
    { Name = s.Name, Type = s.Template?.GetType() ?? Type.GetType(s.TypeName, throwOnError: true)!, Creation = s.Creation, Template = s.Template }).ToArray();
    public void Apply(UINode node)
    {
        foreach (var binding in Bindings.Where(b => b.Enabled))
        {
            var control = GetParent().GetNode<Control>(binding.Target);
            if (binding.Kind == ViewBindingKind.Command) node.BindCommand(binding.Source, ((BaseButton)control).ClickTarget());
            else if (binding.Kind == ViewBindingKind.Collection)
                node.BindCollection(binding.Source, new GodotCollectionTarget(node, control, binding.ItemScene ?? throw new InvalidOperationException("Item scene is required.")), binding.OneTimeCollection);
            else node.Bind(binding.Source, GodotBindingTargets.Property(control, binding.Member), new BindingOptions
            { Mode = binding.Mode, Convert = binding.Converter.Length == 0 ? null : Converters.Find(binding.Converter), ConvertBack = binding.ConvertBack.Length == 0 ? null : Converters.Find(binding.ConvertBack), HasNullValue = binding.HasNullValue, NullValue = binding.NullValue });
        }
    }
}
public static class GodotMvvmExtensions
{
    public static T GetSharedViewModel<T>(this MvvmRuntime runtime, T template) where T : Resource => runtime.GetSharedViewModel(template, new GodotViewModelAssets());
    public static void UseGodotMvvm(this UIContext ui, MvvmRuntime runtime)
    {
        ui.AddNodeExtension(new MvvmNodeExtension(runtime, node => node.Element is GodotUIElement display
            ? display.Control.GetChildren().OfType<MvvmBindings>().SingleOrDefault() : null, new GodotViewModelAssets()));
        if (ui.Host is not GodotUIHost) throw new InvalidOperationException("Expected a Godot host.");
        var tree = (SceneTree)Engine.GetMainLoop();
        GodotMvvmObserver.Attach(tree, runtime);
    }
}
internal static class GodotMvvmObserver
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SceneTree, HashSet<MvvmRuntime>> Runtimes = new();
    internal static void Attach(SceneTree tree, MvvmRuntime runtime)
    {
        if (!Runtimes.TryGetValue(tree, out var runtimes))
        {
            runtimes = new HashSet<MvvmRuntime>(); Runtimes.Add(tree, runtimes);
            tree.ProcessFrame += () => { foreach (var value in runtimes.ToArray()) { if (value.IsDisposed) runtimes.Remove(value); else value.ObserveTasks(); } };
        }
        runtimes.Add(runtime);
    }
}
