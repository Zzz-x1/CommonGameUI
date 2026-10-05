using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo
{
[UIResource("res://Scenes/Client/Login.tscn")]
public partial class LoginView : ClientView
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Wire("LoginCard/Enter", App.EnterAsync);
        Feedback();
        return Task.CompletedTask;
    }

    public new void Text(string path, string text) => base.Text("LoginCard/" + path, text);
    public override void Refresh() => Text("Account", $"本地档案 · {Data.Name}");
}
}
