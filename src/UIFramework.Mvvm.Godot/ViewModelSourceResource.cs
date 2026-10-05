using Godot;
using UIFramework.Mvvm;

namespace UIFramework.Mvvm.Godot;
[Tool, GlobalClass]
public partial class ViewModelSourceResource : Resource
{
    [Export] public string Name { get; set; } = "";
    [Export] public string TypeName { get; set; } = "";
    [Export] public ViewModelCreation Creation { get; set; }
    [Export] public Resource? Template { get; set; }
}
