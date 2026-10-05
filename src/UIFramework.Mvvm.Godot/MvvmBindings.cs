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
                node.BindCollection(binding.Source, new CollectionTarget(node, control, binding.ItemScene ?? throw new InvalidOperationException("Item scene is required.")), binding.OneTimeCollection);
            else node.Bind(binding.Source, BindingTargets.Property(control, binding.Member), Converters.Configure(binding.Converter, binding.ConvertBack, binding.Mode, binding.ConverterInput, binding.ConverterConstant, binding.HasNullValue, binding.NullValue));
        }
    }
}
public static class MvvmExtensions
{
    public static T GetSharedViewModel<T>(this UIContext ui, T template) where T : Resource => ui.GetMvvm().GetSharedViewModel(template, new ViewModelAssets());
    public static T GetSharedViewModel<T>(this MvvmRuntime runtime, T template) where T : Resource => runtime.GetSharedViewModel(template, new ViewModelAssets());

}
internal static class MvvmObserver
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SceneTree, HashSet<MvvmRuntime>> Runtimes = new();
    internal static void Attach(SceneTree tree, MvvmRuntime runtime)
    {
        if (!Runtimes.TryGetValue(tree, out var runtimes))
        {
            runtimes = new HashSet<MvvmRuntime>(); Runtimes.Add(tree, runtimes);
            tree.ProcessFrame += () => { foreach (var value in runtimes.ToArray()) { value.ObserveTasks(); if (value.IsDisposed) runtimes.Remove(value); } };
        }
        runtimes.Add(runtime);
    }
}
