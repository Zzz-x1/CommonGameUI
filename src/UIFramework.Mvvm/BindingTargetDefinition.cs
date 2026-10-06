using System.Reflection;

namespace UIFramework.Mvvm;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class BindingTargetProviderAttribute(Type type) : Attribute
{ public Type Type { get; } = type; }

public interface IBindingTargetProvider
{ IEnumerable<BindingTargetDefinition> GetTargets(); }

public sealed class BindingTargetDefinition
{
    public Type TargetType { get; }
    public string Member { get; }
    public Type ValueType { get; }
    public bool Observable { get; }
    private readonly Func<object, IBindingTarget> _create;
    private BindingTargetDefinition(Type targetType, string member, Type valueType, bool observable, Func<object, IBindingTarget> create)
    { TargetType = targetType; Member = member; ValueType = valueType; Observable = observable; _create = create; }
    public static BindingTargetDefinition Create<TTarget, TValue>(string member, Func<TTarget, TValue> read,
        Action<TTarget, TValue> write, Func<TTarget, Action, Action>? subscribe = null) where TTarget : class =>
        new(typeof(TTarget), member, typeof(TValue), subscribe != null,
            value => new Target<TValue>(() => read((TTarget)value), v => write((TTarget)value, v),
                subscribe == null ? null : callback => subscribe((TTarget)value, callback)));
    public IBindingTarget CreateTarget(object target, BindingMode mode = BindingMode.OneWay)
    {
        if (!TargetType.IsInstanceOfType(target)) throw new InvalidCastException("Binding target type mismatch: " + TargetType.FullName);
        if (!Observable && mode is BindingMode.TwoWay or BindingMode.OneWayToViewModel)
            throw new InvalidOperationException("Binding target does not provide change notifications: " + Member);
        return _create(target);
    }
    private sealed class Target<T> : IBindingTarget
    {
        private readonly Func<T> _read; private readonly Action<T> _write; private Action? _disconnect;
        public Target(Func<T> read, Action<T> write, Func<Action, Action>? subscribe)
        { _read = read; _write = write; _disconnect = subscribe?.Invoke(() => Changed?.Invoke()); }
        public Type ValueType => typeof(T);
        public object? Read() => _read();
        public void Write(object? value) => _write((T)value!);
        public event Action? Changed;
        public void Dispose() { var disconnect = _disconnect; _disconnect = null; disconnect?.Invoke(); }
    }
}

public static class BindingTargetRegistry
{
    private static readonly Dictionary<Assembly, BindingTargetDefinition[]> Providers = new();
    public static IReadOnlyList<BindingTargetDefinition> Members(Type targetType)
    {
        var definitions = Discover().Where(d => d.TargetType.IsAssignableFrom(targetType)).ToArray();
        return definitions.Select(d => d.Member).Distinct(StringComparer.Ordinal)
            .Select(member => Resolve(targetType, member, definitions)).ToArray();
    }
    public static BindingTargetDefinition Find(Type targetType, string member) => Resolve(targetType, member, Discover());
    private static BindingTargetDefinition Resolve(Type type, string member, IEnumerable<BindingTargetDefinition> definitions)
    {
        var matches = definitions.Where(d => d.Member == member && d.TargetType.IsAssignableFrom(type)).ToArray();
        if (matches.GroupBy(d => d.TargetType).Any(g => g.Count() != 1))
            throw new InvalidOperationException("Duplicate binding target definition: " + type.FullName + "." + member);
        var mostSpecific = matches.Where(d => !matches.Any(other => other != d && d.TargetType.IsAssignableFrom(other.TargetType))).ToArray();
        if (mostSpecific.Length == 0) throw new MissingMemberException(type.FullName, member);
        if (mostSpecific.Length != 1) throw new InvalidOperationException("Ambiguous binding target: " + type.FullName + "." + member);
        return mostSpecific[0];
    }
    private static IEnumerable<BindingTargetDefinition> Discover()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            BindingTargetDefinition[] targets;
            lock (Providers)
            {
                if (!Providers.TryGetValue(assembly, out targets!))
                {
                    targets = assembly.GetCustomAttributes<BindingTargetProviderAttribute>()
                        .SelectMany(a => ((IBindingTargetProvider)Activator.CreateInstance(a.Type)!).GetTargets()).ToArray();
                    if (targets.GroupBy(d => (d.TargetType, d.Member)).Any(g => g.Count() != 1))
                        throw new InvalidOperationException("Duplicate binding target definition in " + assembly.FullName);
                    Providers.Add(assembly, targets);
                }
            }
            foreach (var target in targets) yield return target;
        }
    }
}
