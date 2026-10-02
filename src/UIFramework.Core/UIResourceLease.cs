namespace UIFramework;

/// <summary>Holds a preloaded resource without creating a UI instance.</summary>
public sealed class UIResourceLease : IDisposable
{
    private readonly UIContext _context;
    private Action? _release;
    internal UIResourceLease(UIContext context, IUIResource resource, Action release)
    { _context = context; Resource = resource; _release = release; }
    internal IUIResource Resource { get; }
    public void Dispose()
    {
        try
        {
            _context.Host.VerifyAccess(); _context.VerifyMutation();
            var release = _release; _release = null; release?.Invoke();
        }
        catch (Exception ex) { _context.Report(ex, "Release UI resource"); throw; }
    }
}
