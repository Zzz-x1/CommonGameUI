namespace UIFramework;

/// <summary>Backend instance. Borrowed embedded instances must not destroy their owner's resource.</summary>
public interface IUIElement : IAsyncDisposable
{
    void SetVisible(bool visible);
    void SetInteractive(bool interactive);
    void BringToFront();
}

public interface IUIResourceLoader
{
    Task<IUIElement> LoadAsync(string resource, IUIElement? mount, CancellationToken cancellationToken);
}

public interface IUIHost
{
    /// <summary>Fail explicitly if called outside the host UI thread.</summary>
    void VerifyAccess();
    object? CaptureFocus();
    void Focus(IUIElement? element);
    void RestoreFocus(object? focus);
}

public interface IUILog
{
    void Error(Exception exception, string context);
}

public sealed class ConsoleUILog : IUILog
{
    public void Error(Exception exception, string context) => Console.Error.WriteLine($"[UI] {context}\n{exception}");
}

public interface IUITransition
{
    Task PlayAsync(IUIElement? element, bool entering, CancellationToken cancellationToken);
    /// <summary>Restore animation-owned presentation after an aborted operation, without replaying an animation.</summary>
    void Restore(IUIElement? element, bool visible);
}

/// <summary>Explicit default, never a fallback after a failed animation.</summary>
public sealed class InstantTransition : IUITransition
{
    public void Restore(IUIElement? element, bool visible) { }
    public Task PlayAsync(IUIElement? element, bool entering, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UIResourceAttribute : Attribute
{
    public UIResourceAttribute(string resource) => Resource = resource;
    public string Resource { get; }
}

public enum NodeState { Detached, Initializing, Ready, Disposing, Disposed }

/// <summary>Secondary failures remain original exceptions, without wrapping the primary.</summary>
public static class UIFailure
{
    public const string SecondaryExceptionsKey = "UIFramework.SecondaryExceptions";
    internal static void AddSecondary(Exception primary, Exception secondary)
    {
        if (!(primary.Data[SecondaryExceptionsKey] is List<Exception> list))
            primary.Data[SecondaryExceptionsKey] = list = new List<Exception>();
        list.Add(secondary);
    }
    public static IReadOnlyList<Exception> GetSecondaryExceptions(Exception exception) =>
        exception.Data[SecondaryExceptionsKey] is List<Exception> list ? list.AsReadOnly() : Array.Empty<Exception>();
}
