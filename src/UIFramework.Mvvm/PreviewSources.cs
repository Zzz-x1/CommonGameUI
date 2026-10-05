namespace UIFramework.Mvvm;

/// <summary>Editor samples for borrowed sources. A factory must return a fresh preview VM.</summary>
public static class PreviewSources
{
    private static readonly Dictionary<Type, Func<object>> Factories = new();
    public static void Register<T>(Func<T> factory) where T : class => Factories[typeof(T)] = () => factory();
    public static object Create(Type type) => Factories.TryGetValue(type, out var factory) ? factory()
        : throw new InvalidOperationException("Missing preview source: " + type.FullName);
    public static bool Has(Type type) => Factories.ContainsKey(type);
}
