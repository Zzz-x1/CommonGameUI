using Godot;
namespace UIFramework.Mvvm.Godot;
public enum ViewBindingKind { Property, Command, Collection }
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
    public CommandBindingOptions GetCommandOptions() => ParameterMode == CommandParameterMode.SourcePath
        ? new CommandBindingOptions { ParameterPath = ParameterPath }
        : new CommandBindingOptions { Mode = ParameterMode, ConstantText = ParameterMode == CommandParameterMode.Constant && !ParameterIsNull ? ParameterConstant : null };
    public BindingOptions GetOptions(bool preview = false)
    {
        var options = Converters.Configure(Converter, ConvertBack, Mode, ConverterInput, ConverterConstant, HasNullValue, NullValue);
        if (preview) { options.Mode = Mode == BindingMode.OneTime ? BindingMode.OneTime : BindingMode.OneWay; options.ConvertBack = null; }
        return options;
    }
}
