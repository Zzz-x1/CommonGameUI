namespace UIFramework.Mvvm;

public enum ViewBindingKind { Property, Command, Collection }

/// <summary>The serialized instructions for one row. Native references stay in the instance slots.</summary>
public readonly record struct BindingRule
{
    public readonly ViewBindingKind Kind;
    public readonly string Source, Member, Converter, ConvertBack, ConverterConstant, NullValue, ParameterPath;
    public readonly string? ParameterLiteral;
    public readonly BindingMode Mode;
    public readonly ConverterInput Input;
    public readonly CommandParameterMode ParameterMode;
    public readonly bool HasNullValue, OneTimeCollection;

    public BindingRule(ViewBindingKind kind, string source, string member = "", BindingMode mode = BindingMode.OneWay,
        string converter = "", string convertBack = "", ConverterInput input = ConverterInput.ViewModel,
        string converterConstant = "", bool hasNullValue = false, string nullValue = "",
        CommandParameterMode parameterMode = CommandParameterMode.None, string? parameterLiteral = null,
        string parameterPath = "", bool oneTimeCollection = false)
    {
        Kind = kind; Source = source; Member = member; Mode = mode;
        Converter = converter; ConvertBack = convertBack; Input = input; ConverterConstant = converterConstant;
        HasNullValue = hasNullValue; NullValue = nullValue;
        ParameterMode = parameterMode; ParameterLiteral = parameterLiteral; ParameterPath = parameterPath;
        OneTimeCollection = oneTimeCollection;
    }
}

/// <summary>Engine instance slots are looked up for each connection, including Prefab overrides.</summary>
public interface IViewBindingSlots
{
    int Count { get; }
    bool IsEnabled(int slot);
    BindingRule GetRule(int slot);
    object GetTarget(int slot);
    Type CommandTargetType { get; }
    ICommandTarget CreateCommandTarget(object target);
    ICollectionTarget CreateCollectionTarget(IUINode owner, int slot, bool preview);
}

public static class ViewBindingPlans
{
    public static void Apply(UINode node, IViewBindingSlots slots, bool preview = false,
        Action<IBindingTarget>? captureProperty = null, Action<object>? captureCollection = null) =>
        MvvmExtensions.Scope(node).ApplyRows(slots, preview, captureProperty, captureCollection);
}

internal sealed class BindingPlans
{
    private readonly Dictionary<string, string[]> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, Dictionary<string, MvvmMember>> _members = new();
    private readonly Dictionary<(BindingRule, Type), CompiledBinding> _rows = new();
    private int _targetVersion;
    internal string[] Path(string path)
    {
        if (_paths.TryGetValue(path, out var parts)) return parts;
        parts = path.Split('.');
        if (parts.Length < 2 || parts.Any(string.IsNullOrEmpty)) throw new ArgumentException("Use Source.Member paths.", nameof(path));
        _paths.Add(path, parts);
        return parts;
    }
    internal MvvmMember Member(IMvvmObject model, string name, MvvmMemberKind kind)
    {
        var type = model.GetType();
        if (!_members.TryGetValue(type, out var members))
            _members.Add(type, members = model.Describe().ToDictionary(member => member.Name, StringComparer.Ordinal));
        return members.TryGetValue(name, out var member) && member.Kind == kind ? member
            : throw new MissingMemberException(type.FullName, name);
    }
    internal CompiledBinding Resolve(BindingRule rule, Type target)
    {
        var version = BindingTargetRegistry.Version;
        if (_targetVersion != version) { _rows.Clear(); _targetVersion = version; }
        var key = (rule, target);
        if (!_rows.TryGetValue(key, out var compiled))
            _rows.Add(key, compiled = new CompiledBinding(rule, target));
        return compiled;
    }
    internal void Clear() { _rows.Clear(); _paths.Clear(); _members.Clear(); }
}

internal sealed class CompiledBinding
{
    internal readonly HashSet<Type> ValidatedOwners = new();
    internal readonly BindingTargetDefinition? Property;
    internal readonly BindingOptions? Options;
    internal readonly CommandBindingOptions? Command;
    internal CompiledBinding(BindingRule rule, Type target)
    {
        if (rule.Kind == ViewBindingKind.Property)
        {
            Property = BindingTargetRegistry.Find(target, rule.Member);
            Options = Converters.Configure(rule.Converter, rule.ConvertBack, rule.Mode, rule.Input,
                rule.ConverterConstant, rule.HasNullValue, rule.NullValue);
            if (Options.HasConstantValue && !Property.ValueType.IsAssignableFrom(Options.ForwardConverter!.Output))
                throw new InvalidCastException($"Converter {rule.Converter} cannot write to {target.Name}.{rule.Member}.");
        }
        if (rule.Kind == ViewBindingKind.Command)
            Command = rule.ParameterMode == CommandParameterMode.SourcePath
                ? new CommandBindingOptions { ParameterPath = rule.ParameterPath }
                : new CommandBindingOptions { Mode = rule.ParameterMode, ConstantText = rule.ParameterLiteral };
    }
}
