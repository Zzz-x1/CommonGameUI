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
[UIResource("res://Scenes/Client/Stages.tscn")]
public partial class StagesView : ClientPage
{
    public override string Module => "Stages";
}
}
