using System.Globalization;
using System.Reflection;

namespace UIFramework.Mvvm;

public sealed class ConverterInfo
{
    public string Name { get; }
    public Type Input { get; }
    public Type Output { get; }
    public Func<object?, object?> Convert { get; }
    public ConverterInfo(string name, Type input, Type output, Func<object?, object?> convert)
    { Name = name; Input = input; Output = output; Convert = convert; }
}
public interface IConverterProvider { IEnumerable<ConverterInfo> GetConverters(); }
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ConverterProviderAttribute : Attribute
{
    public Type Type { get; }
    public ConverterProviderAttribute(Type type) => Type = type;
}
public static class Converters
{
    public static IEnumerable<ConverterInfo> Discover()
    {
        yield return new ConverterInfo("FormatNumber", typeof(int), typeof(string), v => ((int)v!).ToString("N0", CultureInfo.CurrentCulture));
        yield return new ConverterInfo("PercentText", typeof(float), typeof(string), v => ((float)v!).ToString("P0", CultureInfo.CurrentCulture));
        yield return new ConverterInfo("InvertBoolean", typeof(bool), typeof(bool), v => !(bool)v!);
        yield return new ConverterInfo("IntToFloat", typeof(int), typeof(float), v => (float)(int)v!);
        yield return new ConverterInfo("FloatToInt", typeof(float), typeof(int), v => checked((int)(float)v!));
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var attribute in assembly.GetCustomAttributes<ConverterProviderAttribute>())
                foreach (var converter in ((IConverterProvider)Activator.CreateInstance(attribute.Type)!).GetConverters()) yield return converter;
    }
    public static bool SupportsConstant(Type type) => type == typeof(string) || type.IsEnum ||
        type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) ||
        type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
        type == typeof(long) || type == typeof(ulong) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    public static BindingOptions Configure(string converter, string convertBack, BindingMode mode,
        ConverterInput input, string constant, bool hasNullValue, object? nullValue)
    {
        var options = new BindingOptions { Mode = mode, HasNullValue = hasNullValue, NullValue = nullValue };
        if (converter.Length != 0) options.Convert = Find(converter);
        if (convertBack.Length != 0) options.ConvertBack = Find(convertBack);
        if (input == ConverterInput.Constant)
        {
            if (converter.Length == 0) throw new InvalidOperationException("Constant input requires a converter.");
            if (mode is BindingMode.TwoWay or BindingMode.OneWayToViewModel)
                throw new InvalidOperationException("Constant input cannot be written back to a ViewModel.");
            var type = Discover().Single(c => c.Name == converter).Input;
            if (!SupportsConstant(type)) throw new NotSupportedException("Converter input does not support a literal: " + type.FullName);
            options.HasConstantValue = true;
            options.ConstantValue = type.IsEnum ? Enum.Parse(type, constant, false) :
                System.Convert.ChangeType(constant, type, CultureInfo.InvariantCulture);
        }
        return options;
    }
    public static Func<object?, object?> Find(string name) => Discover().Single(c => c.Name == name).Convert;
}
