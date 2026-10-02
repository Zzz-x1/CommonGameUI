namespace UIFramework;

/// <summary>Backend instance. Borrowed embedded instances must not destroy their owner's resource.</summary>
public interface IUIElement : IAsyncDisposable
{
    void SetVisible(bool visible);
    void SetInteractive(bool interactive);
    void BringToFront();
    object CaptureOrder();
    void RestoreOrder(object order);
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
    IUITransitionRun Create(TransitionContext context);
}

public interface IUITransitionRun
{
    Task PlayAsync(CancellationToken cancellationToken);
    /// <summary>Release temporary presentation on success or failure. Must not replay animation.</summary>
    void Restore();
}

public enum TransitionDirection { None, Forward, Backward }

public sealed class TransitionContext
{
    public TransitionContext(LifecycleContext lifecycle, bool entering, TransitionDirection direction)
    { Lifecycle = lifecycle; Entering = entering; Direction = direction; }
    public LifecycleContext Lifecycle { get; }
    public bool Entering { get; }
    public TransitionDirection Direction { get; }
    public UINode Node => Lifecycle.Node;
    public IUIElement? Element => Node.Element;
    public ContainerNode? Container => Lifecycle.Container;
    public UINode? Source => Lifecycle.Source;
    public UINode? Target => Lifecycle.Target;
    public LifecycleReason Reason => Lifecycle.Reason;
}

/// <summary>Explicit default, never a fallback after a failed animation.</summary>
public sealed class InstantTransition : IUITransition, IUITransitionRun
{
    public IUITransitionRun Create(TransitionContext context) => this;
    public void Restore() { }
    public Task PlayAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public enum ModalBackdropMode { Shared, PerModal }

/// <summary>Creates an owned display. Backend must observe every dismiss-request task.</summary>
public interface IUIBackdropFactory
{
    Task<IUIBackdrop> CreateAsync(IUIElement? mount, Func<Task> requestDismiss, CancellationToken token);
}

public interface IUIBackdrop : IUIElement
{
    Task ShowAsync(CancellationToken token);
    Task HideAsync(CancellationToken token);
    void Restore(bool visible);
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UIResourceAttribute : Attribute
{
    public UIResourceAttribute(string resource) => Resource = resource;
    public string Resource { get; }
    public WindowInstanceMode WindowMode { get; set; } = WindowInstanceMode.Single;
}

public enum WindowInstanceMode { Single, Multiple }

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
