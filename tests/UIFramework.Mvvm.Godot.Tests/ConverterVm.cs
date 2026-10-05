using Godot;
using UIFramework.Mvvm;
namespace NativeMvvm.Tests;
[Tool, ViewModel, GlobalClass]
public partial class ConverterVm : Resource
{
    [Export] public int InitialGold { get; set; } = 10;
    [FieldNotify] public int Gold { get => InitialGold; set => InitialGold = value; }
}
