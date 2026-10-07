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
    Task<IUIResource> LoadAsync(string resource, CancellationToken cancellationToken);
}

/// <summary>An acquired, instantiable resource. Instantiation runs on the UI thread.</summary>
public interface IUIResource : IDisposable
{
    IUIElement Instantiate(IUIElement? mount);
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
public enum TransitionPhase { Enter, Exit, PushEnter, PushExit, PopEnter, PopExit }
public enum TransitionStage { Preparing, Playing, Completed, Canceled, Failed }

/// <summary>One item's transition snapshot. Progress describes animation time, not resource loading.</summary>
public sealed class TransitionState
{
    internal TransitionState(TransitionContext context, TransitionStage stage, float progress)
    { Context = context; Stage = stage; Progress = progress; }
    public TransitionContext Context { get; }
    public TransitionStage Stage { get; }
    public float Progress { get; }
    public bool IsRunning => Stage == TransitionStage.Preparing || Stage == TransitionStage.Playing;
}

public sealed class TransitionContext
{
    private Action<float>? _reportProgress;
    private bool _managed;
    public TransitionContext(LifecycleContext lifecycle, bool entering, TransitionDirection direction)
    { Lifecycle = lifecycle; Entering = entering; Direction = direction; }
    public LifecycleContext Lifecycle { get; }
    public bool Entering { get; }
    public TransitionDirection Direction { get; }
    public TransitionPhase Phase => (Direction, Entering) switch
    {
        (TransitionDirection.Forward, true) => TransitionPhase.PushEnter,
        (TransitionDirection.Forward, false) => TransitionPhase.PushExit,
        (TransitionDirection.Backward, true) => TransitionPhase.PopEnter,
        (TransitionDirection.Backward, false) => TransitionPhase.PopExit,
        (TransitionDirection.None, true) => TransitionPhase.Enter,
        (TransitionDirection.None, false) => TransitionPhase.Exit,
        _ => throw new InvalidOperationException("Unknown transition direction.")
    };
    public IUINode Node => Lifecycle.Node;
    public IUIElement? Element => Node.Element;
    public ContainerNode? Container => Lifecycle.Container;
    public IUINode? Source => Lifecycle.Source;
    public IUINode? Target => Lifecycle.Target;
    public LifecycleReason Reason => Lifecycle.Reason;
    /// <summary>Custom runs report normalized, monotonic animation time. Easing must not alter this value.</summary>
    public void ReportProgress(float progress)
    {
        try
        {
            Node.Context.Host.VerifyAccess();
            Node.Context.VerifyMutation();
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0 || progress > 1)
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (_managed && _reportProgress == null)
                throw new InvalidOperationException("Progress can only be reported during playback.");
            _reportProgress?.Invoke(progress);
        }
        catch (Exception ex) { Node.Node.ReportFailure(ex, "Report transition progress"); throw; }
    }
    internal void SetProgressReporter(Action<float>? report) { _managed = true; _reportProgress = report; }
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
    Task ShowAsync(bool playAnimation, CancellationToken token);
    Task HideAsync(bool playAnimation, CancellationToken token);
    void Restore(bool visible);
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UIResourceAttribute : Attribute
{
    public UIResourceAttribute(string resource) => Resource = resource;
    public string Resource { get; }
    public WindowInstanceMode WindowMode { get; set; } = WindowInstanceMode.Single;
    public NodeCacheMode CacheMode { get; set; } = NodeCacheMode.None;
}

public enum NodeCacheMode { None, KeepAlive }

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
