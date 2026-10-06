using System.Globalization;

namespace UIFramework.Mvvm;
public enum CommandParameterMode { None, Constant, SourcePath }
public sealed class CommandBindingOptions
{
    public CommandParameterMode Mode { get; set; }
    private string _path = "";
    public string ParameterPath { get => _path; set { _path = value; Mode = CommandParameterMode.SourcePath; } }
    public object? ConstantValue { get; set; }
    public string? ConstantText { get; set; }
}
public static class CommandParameters
{
    public static object? ParseConstant(string text, Type type)
    {
        if (!Converters.SupportsConstant(type)) throw new NotSupportedException("Command parameter does not support a literal: " + type.FullName);
        return type.IsEnum ? Enum.Parse(type, text, false) : Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
    }
    public static void Validate(Type type, object? value)
    {
        if (type == typeof(void)) { if (value != null) throw new ArgumentException("Command does not accept a business parameter."); return; }
        if (value == null)
        { if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) throw new ArgumentException("Command parameter cannot be null: " + type.FullName); }
        else if (!type.IsInstanceOfType(value)) throw new InvalidCastException("Command parameter type mismatch: " + type.FullName);
    }
}
