using Godot;
using System.Reflection;
using UIFramework.Mvvm.Godot;
namespace HierarchyDemo;
[Tool]
public partial class MvvmEditorVerification : global::Godot.Node
{
    private Task? _work;
    public override void _Ready() { if (Engine.IsEditorHint() && OS.GetCmdlineUserArgs().Contains("--mvvm-editor-test")) _work = Verify(); }
    public override void _Process(double delta)
    {
        if (_work?.IsCompleted != true) return; SetProcess(false);
        try { _work.GetAwaiter().GetResult(); GD.Print("MVVM GODOT EDITOR: source GUI, native Undo and binding configuration passed"); GetTree().Quit(); }
        catch (Exception error) { GD.PrintErr(error); GetTree().Quit(1); }
    }
    private async Task Verify()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var plugin = Find(GetTree().Root).OfType<MvvmEditorPlugin>().Single();
        var view = new Control { Name = "View" }; AddChild(view); view.Owner = this;
        var config = new MvvmBindings { Name = "MvvmBindings" }; view.AddChild(config); config.Owner = this;
        config.ViewModels.Add(new ViewModelSourceResource { Name = "Player", Template = new HierarchyDemo.Tests.NativePlayerVm(), Creation = UIFramework.Mvvm.ViewModelCreation.SharedAsset });
        var selection = EditorInterface.Singleton.GetSelection(); selection.Clear(); selection.AddNode(config);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var sources = (Tree)typeof(MvvmEditorPlugin).GetField("_sources", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        sources.GetRoot().GetFirstChild().Select(0); sources.EmitSignal(Tree.SignalName.ItemSelected);
        var details = (VBoxContainer)typeof(MvvmEditorPlugin).GetField("_details", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        var name = details.GetChildren().OfType<LineEdit>().First(); name.Text = "Renamed"; name.EmitSignal(LineEdit.SignalName.TextSubmitted, "Renamed");
        Check(config.ViewModels[0].Name == "Renamed", "GUI did not update source");
        var manager = plugin.GetUndoRedo(); manager.GetHistoryUndoRedo(manager.GetObjectHistoryId(view)).Undo(); Check(config.ViewModels[0].Name == "Player", "Native Undo did not restore source");
        var label = new Label { Name = "Gold" }; view.AddChild(label); label.Owner = this; plugin.BindControl(label, "Text");
        Check(config.Bindings.Count == 1 && config.GetTarget(config.Bindings[0]) == label, "Inspector binding entry");
        var bindings = (Tree)typeof(MvvmEditorPlugin).GetField("_bindings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        bindings.GetRoot().GetFirstChild().Select(0);
        typeof(MvvmEditorPlugin).GetMethod("Delete", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
        Check(config.Bindings.Count == 0, "Delete GUI binding");
        var other = new Control { Name = "Other" }; AddChild(other);
        var otherConfig = new MvvmBindings(); other.AddChild(otherConfig);
        selection.Clear(); selection.AddNode(otherConfig);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        manager.GetHistoryUndoRedo(manager.GetObjectHistoryId(view)).Undo();
        Check(config.Bindings.Count == 1 && otherConfig.Bindings.Count == 0, "Undo after selection change targeted wrong view");
        other.QueueFree();
        selection.Clear(); selection.AddNode(config);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        config.ViewModels.Add(new ViewModelSourceResource { Name = "Manual", Template = null, Creation = UIFramework.Mvvm.ViewModelCreation.Manual });
        typeof(MvvmEditorPlugin).GetMethod("StartPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
        var status = (Label)typeof(MvvmEditorPlugin).GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        Check(status.Text.Contains("缺少 VM 资产：Manual"), "Missing Manual preview source was not displayed");
        config.ViewModels.RemoveAt(1);
        config.Owner = view; label.Owner = view;
        var packed = new PackedScene(); Check(packed.Pack(view) == Error.Ok, "Pack GUI configuration");
        var restored = packed.Instantiate<Control>(); Check(restored.GetNode<MvvmBindings>("MvvmBindings").Bindings.Count == 1, "Reopen GUI configuration"); restored.Free();
        selection.Clear(); view.QueueFree();
    }
    private static IEnumerable<global::Godot.Node> Find(global::Godot.Node root) { yield return root; foreach (var child in root.GetChildren()) foreach (var node in Find(child)) yield return node; }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
