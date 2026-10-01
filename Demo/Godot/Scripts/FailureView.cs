using UIFramework;

namespace HierarchyDemo;

public class DemoMenuNode : UINode
{
    protected override bool InitiallyVisible => true;
}

/// <summary>Intentional failure fixture, never registered by the normal Demo.</summary>
public class FailureView : UINode
{
    private readonly string _mode;
    public FailureView() : this("load") { }
    public FailureView(string mode) => _mode = mode;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        if (_mode == "lifecycle") throw new InvalidOperationException("Intentional original lifecycle exception.");
        if (_mode == "transition") Transition = new BrokenTransition();
        return Task.CompletedTask;
    }
    private sealed class BrokenTransition : IUITransition
    {
        public Task PlayAsync(IUIElement? element, bool entering, CancellationToken token) =>
            throw new InvalidOperationException("Intentional original transition exception.");
    }
}
