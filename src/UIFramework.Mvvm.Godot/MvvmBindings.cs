using Godot;
using UIFramework.Godot;

namespace UIFramework.Mvvm.Godot;
[Tool, GlobalClass]
public partial class MvvmBindings : global::Godot.Node, IViewBindingConfiguration
{
    [Export] public global::Godot.Collections.Array<ViewModelSourceResource> ViewModels { get; set; } = new();
    [Export] public global::Godot.Collections.Array<ViewBindingResource> Bindings { get; set; } = new();
    [Export] public global::Godot.Collections.Array<Control> Targets { get; set; } = new();
    [Export] public global::Godot.Collections.Array<global::Godot.Node> ItemFactories { get; set; } = new();
    public void AddBinding(Control target, ViewBindingResource binding) { EnsureFactories(); Targets.Add(target); Bindings.Add(binding); ItemFactories.Add(null!); }
    public void EnsureFactories() { if (ItemFactories.Count == 0) for (int i = 0; i < Bindings.Count; i++) ItemFactories.Add(null!); if (ItemFactories.Count != Bindings.Count) throw new InvalidOperationException("Each binding must have a factory reference slot."); }
    public global::Godot.Node? GetFactory(ViewBindingResource binding) { if (ItemFactories.Count == 0) return null; if (ItemFactories.Count != Bindings.Count) throw new InvalidOperationException("Each binding must have a factory reference slot."); return ItemFactories[Bindings.IndexOf(binding)]; }
    public CollectionTarget GetCollectionTarget(IUINode node, ViewBindingResource binding, bool preview = false) =>
        new(node, GetTarget(binding), binding.ItemScene, preview, GetFactory(binding) is { } factory ? (ICollectionItemFactory)factory : null, binding.ItemSources.Select(s => s.ToMapping()).ToArray());
    public Control GetTarget(ViewBindingResource binding)
    {
        if (Targets.Count != Bindings.Count) throw new InvalidOperationException("Each binding must have a native target reference.");
        var target = Targets[Bindings.IndexOf(binding)];
        return GodotObject.IsInstanceValid(target) ? target : throw new InvalidOperationException("Missing binding target: " + binding.Source);
    }
    public IReadOnlyList<ViewModelSource> Sources => ViewModels.Select(s => new ViewModelSource
    { Name = s.Name, Type = s.Template?.GetType() ?? throw new InvalidOperationException("VM asset is required: " + s.Name), Creation = s.Creation, Template = s.Template }).ToArray();
    public void Apply(UINode node) => Apply(node, false);
    public void Apply(UINode node, bool preview)
    {
        foreach (var binding in Bindings.Where(b => b.Enabled))
        {
            var control = GetTarget(binding);
            if (binding.Kind == ViewBindingKind.Command) { if (preview) continue; node.BindCommand(binding.Source, ((BaseButton)control).ClickTarget(), binding.GetCommandOptions()); }
            else if (binding.Kind == ViewBindingKind.Collection)
                node.BindCollection(binding.Source, GetCollectionTarget(node, binding, preview), binding.OneTimeCollection);
            else { var options = binding.GetOptions(preview); node.Bind(binding.Source, BindingTargets.Property(control, binding.Member, options.Mode), options); }
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
