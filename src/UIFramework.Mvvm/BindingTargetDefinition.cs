namespace UIFramework.Mvvm;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class BindingTargetProviderAttribute(Type type) : Attribute
{ public Type Type { get; } = type; }

public interface IBindingTargetProvider
{ IEnumerable<BindingTargetDefinition> GetTargets(); }

internal interface IRegisteredBindingTarget
{
    void ValidateMode(BindingMode mode);
}

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
            value => new Target<TValue>(member, () => read((TTarget)value), v => write((TTarget)value, v),
                subscribe == null ? null : callback => subscribe((TTarget)value, callback)));

    public bool Supports(BindingMode mode) => Supports(mode, Observable);

    private static bool Supports(BindingMode mode, bool observable) => mode switch
    {
        BindingMode.OneTime or BindingMode.OneWay => true,
        BindingMode.OneWayToViewModel or BindingMode.TwoWay => observable,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public IBindingTarget CreateTarget(object target, BindingMode mode = BindingMode.OneWay)
    {
        if (!TargetType.IsInstanceOfType(target)) throw new InvalidCastException("Binding target type mismatch: " + TargetType.FullName);
        if (!Supports(mode)) throw MissingNotifications(Member);
        return _create(target);
    }

    private static InvalidOperationException MissingNotifications(string member) =>
        new("Binding target does not provide change notifications: " + member);

    private sealed class Target<T>(string member, Func<T> read, Action<T> write,
        Func<Action, Action>? subscribe) : IBindingTarget, IRegisteredBindingTarget
    {
        private Action? _changed;
        private Action? _disconnect;
        private bool _disposed;

        public Type ValueType => typeof(T);
        public object? Read() => read();
        public void Write(object? value) => write((T)value!);

        public void ValidateMode(BindingMode mode)
        {
            if (!Supports(mode, subscribe != null)) throw MissingNotifications(member);
        }

        public event Action? Changed
        {
            add
            {
                if (value == null) return;
                if (_disposed) throw new ObjectDisposedException(nameof(IBindingTarget));
                if (subscribe == null) throw MissingNotifications(member);
                // A control may report its initial value while the listener is being installed.
                if (_changed == null) _disconnect = subscribe(() => _changed?.Invoke());
                _changed += value;
            }
            remove
            {
                _changed -= value;
                if (_changed == null) Disconnect();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _changed = null;
            Disconnect();
        }

        private void Disconnect()
        {
            var disconnect = _disconnect;
            _disconnect = null;
            disconnect?.Invoke();
        }
    }
}

public static class BindingTargetRegistry
{
    private static readonly List<BindingTargetDefinition> Definitions = new();
    private static readonly HashSet<Type> Providers = new();
    private static readonly Dictionary<(Type, string), BindingTargetDefinition> Cache = new();
    internal static int Version => System.Threading.Volatile.Read(ref _version);
    private static int _version;

    public static void Register(IBindingTargetProvider provider)
    {
        lock (Definitions)
        {
            if (Providers.Contains(provider.GetType())) return;
            var added = provider.GetTargets().ToArray();
            var duplicate = Definitions.Concat(added).GroupBy(d => (d.TargetType, d.Member)).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException("Duplicate binding target: " + duplicate.Key);
            Definitions.AddRange(added);
            Providers.Add(provider.GetType());
            Cache.Clear();
            System.Threading.Interlocked.Increment(ref _version);
        }
    }
    public static IReadOnlyList<BindingTargetDefinition> Members(Type targetType)
    {
        lock (Definitions)
            return Definitions.Where(d => d.TargetType.IsAssignableFrom(targetType)).Select(d => d.Member)
                .Distinct(StringComparer.Ordinal).Select(member => Find(targetType, member)).ToArray();
    }
    public static BindingTargetDefinition Find(Type targetType, string member)
    {
        lock (Definitions)
        {
            var key = (targetType, member);
            if (!Cache.TryGetValue(key, out var definition))
                Cache.Add(key, definition = Resolve(targetType, member, Definitions));
            return definition;
        }
    }
    private static BindingTargetDefinition Resolve(Type type, string member, IEnumerable<BindingTargetDefinition> definitions)
    {
        var matches = definitions.Where(d => d.Member == member && d.TargetType.IsAssignableFrom(type)).ToArray();
        var mostSpecific = matches.Where(d => !matches.Any(other => other != d && d.TargetType.IsAssignableFrom(other.TargetType))).ToArray();
        if (mostSpecific.Length == 0) throw new MissingMemberException(type.FullName, member);
        if (mostSpecific.Length != 1) throw new InvalidOperationException("Ambiguous binding target: " + type.FullName + "." + member);
        return mostSpecific[0];
    }
}
