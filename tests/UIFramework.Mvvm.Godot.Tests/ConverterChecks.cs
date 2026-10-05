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
        var template = new ConverterVm();
        Check(ResourceSaver.Save(template, "res://Player.tres") == Error.Ok, "VM asset save");
        view.ViewModels.Add(new ViewModelSourceResource { Name = "Player", Template = template });
        view.Bindings.Add(new ViewBindingResource { Target = new NodePath("Gold"), Member = "Text", Source = "Player.Gold", Converter = "FormatNumber", ConverterInput = ConverterInput.Constant, ConverterConstant = "125" });
        await using (var preview = new BindingPreview(view)) Check(label.Text == "125", "constant preview");
        Check(label.Text == "before preview" && template.Gold == 10, "preview restore / original asset");
        var packed = new PackedScene(); Check(packed.Pack(root) == Error.Ok, "scene pack");
        Check(ResourceSaver.Save(packed, "res://Window.tscn") == Error.Ok, "scene save");
        var clone = ResourceLoader.Load<PackedScene>("res://Window.tscn", cacheMode: ResourceLoader.CacheMode.Ignore).Instantiate<Control>(); AddChild(clone);
        var saved = clone.GetNode<MvvmBindings>("MvvmBindings").Bindings[0];
        Check(saved.Converter == "FormatNumber" && saved.ConverterInput == ConverterInput.Constant && saved.ConverterConstant == "125", "converter scene reload");
        await using (var preview = new BindingPreview(clone.GetNode<MvvmBindings>("MvvmBindings"))) Check(clone.GetNode<Label>("Gold").Text == "125", "reloaded scene preview");
        Check(clone.GetNode<Label>("Gold").Text == "before preview", "reloaded scene restore");
        root.QueueFree(); clone.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        File.WriteAllText("converter-checks.txt", "Passed: asset save, constant preview, restoration, original asset, scene save/reload, cloned scene preview/restoration");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
