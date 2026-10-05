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
    public static Func<object?, object?> Find(string name) => Discover().Single(c => c.Name == name).Convert;
}
