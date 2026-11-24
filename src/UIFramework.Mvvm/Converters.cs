using System.Globalization;

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
    private static readonly Dictionary<string, (ConverterInfo Info, string Source)> Entries =
        Builtins().ToDictionary(c => c.Name, c => (c, "UIFramework.Mvvm"), StringComparer.Ordinal);
    private static readonly HashSet<Type> Providers = new();

    public static void Register(IConverterProvider provider)
    {
        lock (Entries)
        {
            var type = provider.GetType();
            if (Providers.Contains(type)) return;
            var source = type.AssemblyQualifiedName!;
            var added = provider.GetConverters().ToArray();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var converter in added)
            {
                if (Entries.TryGetValue(converter.Name, out var existing) || !names.Add(converter.Name))
                    throw new InvalidOperationException($"Duplicate converter '{converter.Name}' in {existing.Source ?? source} and {source}.");
            }
            foreach (var converter in added) Entries.Add(converter.Name, (converter, source));
            Providers.Add(type);
        }
    }
    public static IEnumerable<ConverterInfo> Discover()
    {
        lock (Entries) return Entries.Values.Select(entry => entry.Info).ToArray();
    }
    private static ConverterInfo Lookup(string name)
    {
        lock (Entries) return Entries.TryGetValue(name, out var entry) ? entry.Info
            : throw new InvalidOperationException("Unknown converter: " + name);
    }
    private static IEnumerable<ConverterInfo> Builtins()
    {
        yield return new ConverterInfo("FormatNumber", typeof(int), typeof(string), v => ((int)v!).ToString("N0", CultureInfo.CurrentCulture));
        yield return new ConverterInfo("PercentText", typeof(float), typeof(string), v => ((float)v!).ToString("P0", CultureInfo.CurrentCulture));
        yield return new ConverterInfo("InvertBoolean", typeof(bool), typeof(bool), v => !(bool)v!);
        yield return new ConverterInfo("IntToFloat", typeof(int), typeof(float), v => (float)(int)v!);
        yield return new ConverterInfo("FloatToInt", typeof(float), typeof(int), v => checked((int)(float)v!));
    }
    public static bool SupportsConstant(Type type) => type == typeof(string) || type.IsEnum ||
        type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) ||
        type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
        type == typeof(long) || type == typeof(ulong) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    public static BindingOptions Configure(string converter, string convertBack, BindingMode mode,
        ConverterInput input, string constant, bool hasNullValue, object? nullValue)
    {
        var options = new BindingOptions { Mode = mode, HasNullValue = hasNullValue, NullValue = nullValue };
        if (converter.Length != 0) { options.ForwardConverter = Lookup(converter); options.Convert = options.ForwardConverter.Convert; }
        if (convertBack.Length != 0) { options.BackwardConverter = Lookup(convertBack); options.ConvertBack = options.BackwardConverter.Convert; }
        if (input == ConverterInput.Constant)
        {
            if (converter.Length == 0) throw new InvalidOperationException("Constant input requires a converter.");
            if (mode is BindingMode.TwoWay or BindingMode.OneWayToViewModel)
                throw new InvalidOperationException("Constant input cannot be written back to a ViewModel.");
            var type = Lookup(converter).Input;
            if (!SupportsConstant(type)) throw new NotSupportedException("Converter input does not support a literal: " + type.FullName);
            options.HasConstantValue = true;
            options.ConstantValue = type.IsEnum ? Enum.Parse(type, constant, false) :
                System.Convert.ChangeType(constant, type, CultureInfo.InvariantCulture);
        }
        return options;
    }
    public static Func<object?, object?> Find(string name) => Lookup(name).Convert;
}
