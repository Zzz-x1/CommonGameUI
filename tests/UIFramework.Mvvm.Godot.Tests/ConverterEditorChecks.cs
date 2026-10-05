using Godot;
using System.Reflection;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;
namespace NativeMvvm.Tests;
[Tool]
public partial class ConverterEditorChecks : MvvmEditorPlugin
{
    public override void _EnterTree() { base._EnterTree(); CallDeferred(nameof(OpenScene)); }
    private void OpenScene() { EditorInterface.Singleton.OpenSceneFromPath("res://Window.tscn"); CallDeferred(nameof(CheckEditor)); }
    private void CheckEditor()
    {
        var root = EditorInterface.Singleton.GetEditedSceneRoot();
        var view = root.GetNode<MvvmBindings>("MvvmBindings");
        Field("_view").SetValue(this, view); Invoke("Refresh");
        var tree = (Tree)Field("_bindings").GetValue(this)!;
        tree.GetRoot().GetFirstChild().Select(0); Invoke("BindingDetails");
        var binding = view.Bindings[0];
        Check(binding.ConverterInput == ConverterInput.Constant, "loaded converter input");
        var details = (VBoxContainer)Field("_details").GetValue(this)!;
        var labels = details.GetChildren().OfType<Label>().ToArray();
        var title = labels.Single(label => label.Text == "输入常量");
        var field = (LineEdit)details.GetChild(title.GetIndex() + 1);
        field.Text = "250"; field.EmitSignal(Control.SignalName.FocusExited);
        Check(binding.ConverterConstant == "250", "GUI parameter edit");
        Check(tree.GetSelected() != null, "selection lost after edit");
        var undo = GetUndoRedo().GetHistoryUndoRedo(GetUndoRedo().GetObjectHistoryId(root));
        undo.Undo(); Check(binding.ConverterConstant == "125", "GUI Undo");
        usingPreview();
        File.WriteAllText(ProjectSettings.GlobalizePath("res://converter-editor-checks.txt"), "Passed: converter parameter GUI, selection after editing, native Undo, preview and restore");
        GetTree().Quit();
        void usingPreview()
        {
            var label = root.GetNode<Label>("Gold");
            var original = label.Text; var preview = new BindingPreview(view);
            Check(label.Text == "125", "editor preview converter");
            var disposal = preview.DisposeAsync(); Check(disposal.IsCompleted, "preview disposal incomplete"); disposal.GetAwaiter().GetResult();
            Check(label.Text == original, "editor preview restore");
        }
    }
    private static FieldInfo Field(string name) => typeof(MvvmEditorPlugin).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private void Invoke(string name) => typeof(MvvmEditorPlugin).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, null);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
