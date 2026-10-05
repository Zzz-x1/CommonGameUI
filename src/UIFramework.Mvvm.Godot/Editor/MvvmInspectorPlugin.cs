using Godot;
namespace UIFramework.Mvvm.Godot;
[Tool]
public partial class MvvmInspectorPlugin : EditorInspectorPlugin
{
    public MvvmEditorPlugin Plugin { get; set; } = null!;
    public override bool _CanHandle(GodotObject @object) => @object is Control;
    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (name is "text" or "value" or "button_pressed" or "visible")
        { var button = new Button { Text = "MVVM · 绑定 " + name }; button.Pressed += () => Plugin.BindControl((Control)@object, name switch { "text" => "Text", "value" => "Value", "button_pressed" => "ButtonPressed", _ => "Visible" }); AddCustomControl(button); }
        return false;
    }
}
