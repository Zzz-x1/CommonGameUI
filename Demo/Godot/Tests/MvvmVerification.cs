using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;
namespace HierarchyDemo.Tests;
public partial class MvvmVerification : global::Godot.Node
{
    private Task _test = null!;
    private int _checks;
    public override void _Ready() => _test = Run();
    public override void _Process(double delta)
    {
        if (!_test.IsCompleted) return; SetProcess(false);
        try { _test.GetAwaiter().GetResult(); GD.Print($"MVVM GODOT: {_checks} checks passed"); GetTree().Quit(); }
        catch (Exception error) { GD.PrintErr(error); GetTree().Quit(1); }
    }
    private void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); _checks++; }
    private async Task Run()
    {
        var template = new NativePlayerVm(); template.Other = template;
        var view = new Control { Name = "View" }; AddChild(view); var label = new Label { Name = "Gold", Text = "original" }; view.AddChild(label);
        var button = new Button { Name = "Buy" }; view.AddChild(button); var slider = new HSlider { Name = "Amount", MinValue = 0, MaxValue = 200 }; view.AddChild(slider);
        var config = new MvvmBindings { Name = "MvvmBindings" }; view.AddChild(config); config.Owner = view; label.Owner = view; button.Owner = view; slider.Owner = view;
        config.ViewModels.Add(new ViewModelSourceResource { Name = "Player", Creation = ViewModelCreation.SharedAsset, Template = template });
        config.AddBinding(view.GetNode<Control>("Gold"), new ViewBindingResource { Member = "Text", Source = "Player.Gold", Converter = "FormatNumber" });
        config.AddBinding(view.GetNode<Control>("Buy"), new ViewBindingResource { Kind = ViewBindingKind.Command, Source = "Player.Buy" });
        config.AddBinding(view.GetNode<Control>("Amount"), new ViewBindingResource { Member = "Value", Source = "Player.Gold", Mode = BindingMode.TwoWay, Converter = "IntToFloat", ConvertBack = "FloatToInt" });
        var runtime = new MvvmRuntime(); var ui = EngineUI.UI.Create(this); ui.ConfigureMvvm(runtime); var node = new UINode(); await node.AttachToAsync(ui.Root, "View", view);
        var vm = node.GetViewModel<NativePlayerVm>("Player"); Check(vm != template, "Original asset was used"); Check(ReferenceEquals(vm, vm.Other), "Asset cycle not preserved"); Check(label.Text == "100", "Initial display missing");
        button.EmitSignal(BaseButton.SignalName.Pressed); Check(vm.Gold == 90 && template.Gold == 100, "Purchase modified template");
        slider.Value = 70; Check(vm.Gold == 70 && label.Text == "70", "Two-way editor"); vm.Items.Add(2); Check(template.Items.Count == 1, "Typed arrays shared storage");
        await node.DisposeAsync(); vm.Gold = 40; Check(label.Text == "70", "Closed node retained bindings"); await ui.DisposeAsync(); await runtime.DisposeAsync();
        await using (var preview = new EngineMvvm.BindingPreview(config)) { Check(label.Text == "100", "Preview initial value"); Check(slider.Value == 100, "Preview slider value"); button.EmitSignal(BaseButton.SignalName.Pressed); Check(template.Gold == 100, "Preview command changed asset"); }
        Check(label.Text == "70" && slider.Value == 70, "Preview did not restore controls");
        template.Other = null; var packed = new PackedScene(); Check(packed.Pack(view) == Error.Ok, "Pack view"); var path = "user://mvvm-config-verification.tscn"; Check(ResourceSaver.Save(packed, path) == Error.Ok, "Save configuration");
        using var saved = ResourceLoader.Load<PackedScene>(path, cacheMode: ResourceLoader.CacheMode.Ignore); var restored = saved.Instantiate<Control>(); var restoredConfig = restored.GetNode<MvvmBindings>("MvvmBindings");
        Check(restoredConfig.ViewModels.Count == 1 && restoredConfig.Bindings.Count == 3, "Configuration was not serialized"); restored.Free(); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        view.QueueFree(); template.Dispose();
    }
}
