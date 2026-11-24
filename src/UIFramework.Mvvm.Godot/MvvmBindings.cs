using Godot;
using UIFramework.Godot;

namespace UIFramework.Mvvm.Godot;
[Tool, GlobalClass]
public partial class MvvmBindings : global::Godot.Node, IViewBindingConfiguration, IViewBindingSlots
{
    [Export] public global::Godot.Collections.Array<ViewModelSourceResource> ViewModels { get; set; } = new();
    [Export] public global::Godot.Collections.Array<ViewBindingResource> Bindings { get; set; } = new();
    [Export] public global::Godot.Collections.Array<Control> Targets { get; set; } = new();
    [Export] public global::Godot.Collections.Array<global::Godot.Node> ItemFactories { get; set; } = new();
    public void AddBinding(Control target, ViewBindingResource binding) { EnsureFactories(); Targets.Add(target); Bindings.Add(binding); ItemFactories.Add(null!); }
    public void EnsureFactories() { if (ItemFactories.Count == 0) for (int i = 0; i < Bindings.Count; i++) ItemFactories.Add(null!); if (ItemFactories.Count != Bindings.Count) throw new InvalidOperationException("Each binding must have a factory reference slot."); }
    public global::Godot.Node? GetFactory(ViewBindingResource binding) { if (ItemFactories.Count == 0) return null; if (ItemFactories.Count != Bindings.Count) throw new InvalidOperationException("Each binding must have a factory reference slot."); return ItemFactories[Bindings.IndexOf(binding)]; }
    int IViewBindingSlots.Count => Bindings.Count;
    bool IViewBindingSlots.IsEnabled(int slot) => Bindings[slot].Enabled;
    BindingRule IViewBindingSlots.GetRule(int slot) => Bindings[slot].Rule;
    Type IViewBindingSlots.CommandTargetType => typeof(BaseButton);
    ICommandTarget IViewBindingSlots.CreateCommandTarget(object target) => ((BaseButton)target).Bindable().Click;
    ICollectionTarget IViewBindingSlots.CreateCollectionTarget(IUINode node, int slot, bool preview)
    {
        var binding = Bindings[slot];
        if (ItemFactories.Count != 0 && ItemFactories.Count != Bindings.Count)
            throw new InvalidOperationException("Each binding must have a factory reference slot.");
        var factory = ItemFactories.Count == 0 ? null : (ICollectionItemFactory?)ItemFactories[slot];
        return new CollectionTarget(node, Targets[slot], binding.ItemScene, preview, factory, binding.ItemSources.Select(s => s.ToMapping()).ToArray());
    }
    object IViewBindingSlots.GetTarget(int slot) => TargetAt(slot);
    public Control GetTarget(ViewBindingResource binding) => TargetAt(Bindings.IndexOf(binding));
    private Control TargetAt(int slot)
    {
        if (Targets.Count != Bindings.Count) throw new InvalidOperationException("Each binding must have a native target reference.");
        var target = Targets[slot];
        return GodotObject.IsInstanceValid(target) ? target : throw new InvalidOperationException("Missing binding target: " + Bindings[slot].Source);
    }
    public IReadOnlyList<ViewModelSource> Sources => ViewModels.Select(s => new ViewModelSource
    { Name = s.Name, Type = s.Template?.GetType() ?? throw new InvalidOperationException("VM asset is required: " + s.Name), Creation = s.Creation, Template = s.Template }).ToArray();
    public void Apply(UINode node) => ViewBindingPlans.Apply(node, this);
}
public static class MvvmExtensions
{
    public static T GetSharedViewModel<T>(this UIContext ui, T template) where T : Resource => ui.GetMvvm().GetSharedViewModel(template, new ViewModelAssets());
    public static T GetSharedViewModel<T>(this MvvmRuntime runtime, T template) where T : Resource => runtime.GetSharedViewModel(template, new ViewModelAssets());

}
