namespace UIFramework;

public interface IUIContextExtension
{
    void Initialize(UIContext context);
    ValueTask DisposeAsync();
}

/// <summary>Optional modules register their context services before contexts are created.</summary>
public static class UIContextExtensions
{
    private static readonly List<Func<IUIContextExtension>> Factories = new();
    public static void Register(Func<IUIContextExtension> factory) => Factories.Add(factory);
    internal static IUIContextExtension[] Create() => Factories.Select(factory => factory()).ToArray();
}
