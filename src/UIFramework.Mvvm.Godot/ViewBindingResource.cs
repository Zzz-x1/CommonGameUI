using Godot;
namespace UIFramework.Mvvm.Godot;
public enum ViewBindingKind { Property, Command, Collection }
[Tool, GlobalClass]
public partial class ViewBindingResource : Resource
{
    [Export] public bool Enabled { get; set; } = true;
    [Export] public NodePath Target { get; set; } = new(".");
    [Export] public string Member { get; set; } = "Text";
    [Export] public string Source { get; set; } = "";
    [Export] public BindingMode Mode { get; set; } = BindingMode.OneWay;
    [Export] public ViewBindingKind Kind { get; set; }
    [Export] public string Converter { get; set; } = "";
    [Export] public string ConvertBack { get; set; } = "";
    [Export] public bool HasNullValue { get; set; }
    [Export] public string NullValue { get; set; } = "";
    [Export] public PackedScene? ItemScene { get; set; }
    [Export] public bool OneTimeCollection { get; set; }
}
