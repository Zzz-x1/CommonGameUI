using Godot;
using UIFramework.Mvvm;
namespace HierarchyDemo.Tests;
[ViewModel]
public partial class NativePlayerVm : Resource
{
    [Export, FieldNotify] public int Gold { get; set; } = 100;
    [Export, FieldNotify] public global::Godot.Collections.Array<int> Items { get; set; } = new() { 1 };
    [Export, FieldNotify] public NativePlayerVm? Other { get; set; }
    [FieldNotify(nameof(Gold))] public bool CanBuy => Gold >= 10;
    [Command(CanExecute = nameof(CanBuy))] public void Buy() => Gold -= 10;
}
