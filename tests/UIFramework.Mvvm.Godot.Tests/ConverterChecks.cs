using Godot;
using UIFramework;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;
namespace NativeMvvm.Tests;
public partial class ConverterChecks : Node
{
    private Task? _run;
    public override void _Ready() => _run = Run();
    public override void _Process(double delta) { if (_run!.IsCompleted) { _run.GetAwaiter().GetResult(); GetTree().Quit(); } }
    private async Task Run()
    {
        var root = new Control { Name = "Window" }; AddChild(root);
        var label = new Label { Name = "Gold", Text = "before preview" }; root.AddChild(label); label.Owner = root;
        var view = new MvvmBindings { Name = "MvvmBindings" }; root.AddChild(view); view.Owner = root;
        using var template = new ConverterVm();
        Check(ResourceSaver.Save(template, "res://Player.tres") == Error.Ok, "VM asset save");
        view.ViewModels.Add(new ViewModelSourceResource { Name = "Player", Template = template });
        view.AddBinding(label, new ViewBindingResource { Member = "Text", Source = "Player.Gold", Converter = "FormatNumber", ConverterInput = ConverterInput.Constant, ConverterConstant = "125" });
        await using (var preview = new BindingPreview(view)) Check(label.Text == "125", "constant preview");
        Check(label.Text == "before preview" && template.Gold == 10, "preview restore / original asset");
        var group = new Control { Name = "Group" }; root.AddChild(group); group.Owner = root;
        label.Reparent(group); label.Name = "RenamedGold";
        Check(view.GetTarget(view.Bindings[0]) == label, "target reference after reparent and rename");
        var packed = new PackedScene(); Check(packed.Pack(root) == Error.Ok, "scene pack");
        Check(ResourceSaver.Save(packed, "res://Window.tscn") == Error.Ok, "scene save");
        using var savedWindow = ResourceLoader.Load<PackedScene>("res://Window.tscn", cacheMode: ResourceLoader.CacheMode.Ignore);
        var clone = savedWindow.Instantiate<Control>(); AddChild(clone);
        var saved = clone.GetNode<MvvmBindings>("MvvmBindings").Bindings[0];
        Check(saved.Converter == "FormatNumber" && saved.ConverterInput == ConverterInput.Constant && saved.ConverterConstant == "125", "converter scene reload");
        var clonedView = clone.GetNode<MvvmBindings>("MvvmBindings");
        Check(clonedView.GetTarget(saved) == clone.GetNode<Label>("Group/RenamedGold"), "target reference remapped to clone");
        Check(clonedView.GetTarget(saved) != label, "target reference points at original scene");
        clonedView.ViewModels[0].Creation = ViewModelCreation.Manual;
        await using (var preview = new BindingPreview(clonedView)) Check(clone.GetNode<Label>("Group/RenamedGold").Text == "125", "reloaded scene preview");
        Check(clone.GetNode<Label>("Group/RenamedGold").Text == "before preview", "reloaded scene restore");
        var presentation = new TargetPresentation { Name = "Presentation" }; root.AddChild(presentation); presentation.Owner = root;
        view.AddBinding(presentation, new ViewBindingResource { Member = "Delay", Source = "Player.Gold", Converter = "IntToFloat" });
        Check(BindingTargetRegistry.Members(presentation.GetType()).Any(d => d.Member == "Delay"), "custom member discovery");
        var buy = new Button { Name = "Buy" }; root.AddChild(buy); buy.Owner = root;
        view.AddBinding(buy, new ViewBindingResource { Kind = ViewBindingKind.Command, Source = "Player.Purchase", ParameterMode = CommandParameterMode.Constant, ParameterConstant = "2" });
        var items = new Control { Name = "Items" }; root.AddChild(items); items.Owner = root;
        var row = new Label { Name = "Row" }; var rowConfig = new MvvmBindings { Name = "MvvmBindings" }; row.AddChild(rowConfig); rowConfig.Owner = row;
        rowConfig.AddBinding(row, new ViewBindingResource { Member = "Text", Source = "Player.Gold", Converter = "FormatNumber" });
        using var scene = new PackedScene(); Check(scene.Pack(row) == Error.Ok, "mapped row pack"); row.Free(); template.Items.Add(template);
        var assets = new ViewModelAssets(); var isolated = (ConverterVm)assets.Clone(template);
        assets.Remap(template, isolated, source => ReferenceEquals(source, template) ? isolated : throw new InvalidOperationException("Unexpected nested asset"));
        Check(ReferenceEquals(isolated.Items[0], isolated), "cyclic container clone"); assets.Release(isolated);
        Check(isolated.Items.Count == 0 && template.Items.Count == 1, "owned container release / original asset preservation");
        var collection = new ViewBindingResource { Kind = ViewBindingKind.Collection, Source = "Player.Items", ItemScene = scene, OneTimeCollection = true }; collection.ItemSources.Add(new CollectionSourceMappingResource { ParentSource = "Player", ItemSource = "Player" }); view.AddBinding(items, collection);
        await using (var preview = new BindingPreview(view))
        { Check(presentation.Delay == 10, "custom target preview"); Check(items.GetChildCount() == 1 && items.GetChild<Label>(0).Text == "10", "mapped item preview"); buy.EmitSignal(BaseButton.SignalName.Pressed); Check(template.Gold == 10, "preview executed parameter command"); }
        Check(presentation.Delay == 7 && items.GetChildCount() == 0, "custom target / list preview restore");
        using var extended = new PackedScene(); Check(extended.Pack(root) == Error.Ok, "extended scene pack"); Check(ResourceSaver.Save(extended, "res://Extended.tscn") == Error.Ok, "extended scene save");
        using var savedExtended = ResourceLoader.Load<PackedScene>("res://Extended.tscn", cacheMode: ResourceLoader.CacheMode.Ignore);
        var reopened = savedExtended.Instantiate<Control>();
        var reopenedBindings = reopened.GetNode<MvvmBindings>("MvvmBindings"); Check(reopenedBindings.Bindings.Last().ItemSources.Single().ItemSource == "Player", "mapped source scene reload"); Check(reopenedBindings.Bindings.Single(b => b.Kind == ViewBindingKind.Command).ParameterConstant == "2", "command parameter scene reload"); reopened.Free(); template.Items.Clear();
        root.QueueFree(); clone.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        File.WriteAllText("converter-checks.txt", "Passed: asset save, constant preview, restoration, original asset, scene save/reload, cloned scene preview/restoration");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
