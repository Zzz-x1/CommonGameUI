using Godot;
namespace UIFramework.Mvvm.Godot;
[Tool, GlobalClass]
public partial class ViewBindingResource : Resource
{
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string Member { get; set; } = "Text";
    [Export] public string Source { get; set; } = "";
    [Export] public BindingMode Mode { get; set; } = BindingMode.OneWay;
    [Export] public ViewBindingKind Kind { get; set; }
    [Export] public string Converter { get; set; } = "";
    [Export] public ConverterInput ConverterInput { get; set; }
    [Export] public string ConverterConstant { get; set; } = "";
    [Export] public string ConvertBack { get; set; } = "";
    [Export] public bool HasNullValue { get; set; }
    [Export] public string NullValue { get; set; } = "";
    [Export] public PackedScene? ItemScene { get; set; }
    [Export] public bool OneTimeCollection { get; set; }
    [Export] public global::Godot.Collections.Array<CollectionSourceMappingResource> ItemSources { get; set; } = new();
    [Export] public bool ParameterIsNull { get; set; }
    [Export] public CommandParameterMode ParameterMode { get; set; }
    [Export] public string ParameterConstant { get; set; } = "";
    [Export] public string ParameterPath { get; set; } = "";
    public BindingRule Rule => new BindingRule(Kind, Source, Member, Mode, Converter, ConvertBack, ConverterInput,
        ConverterConstant, HasNullValue, NullValue, ParameterMode, ParameterIsNull ? null : ParameterConstant,
        ParameterPath, OneTimeCollection);

}
