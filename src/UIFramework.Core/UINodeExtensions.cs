namespace UIFramework;

public static class UINodeExtensions
{
    /// <summary>Wait for the current opening to close, including its exit and cleanup.</summary>
    public static Task WaitForCloseAsync(this IUINode node, CancellationToken cancellationToken = default)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        var state = node.Node;
        try
        {
            state.Context.Host.VerifyAccess();
            cancellationToken.ThrowIfCancellationRequested();
            var session = state.Presentation
                ?? throw new InvalidOperationException("WaitForCloseAsync requires a window opened by Panel, Stack or Modal.");
            for (var frame = UINode.CurrentOperation; frame != null; frame = frame.Previous)
            {
                if (!frame.Active) continue;
                for (var ancestor = state; ancestor != null; ancestor = ancestor.Parent)
                    if (ReferenceEquals(ancestor, frame.Node))
                        throw new InvalidOperationException("Cannot wait for a window to close from its own or an ancestor's active lifecycle.");
                for (var ancestor = frame.Node; ancestor != null; ancestor = ancestor.Parent)
                    if (ReferenceEquals(ancestor, state))
                        throw new InvalidOperationException("Cannot wait for a window to close from a descendant's active lifecycle.");
            }
            return session.WaitForCloseAsync(cancellationToken);
        }
        catch (Exception error) { state.ReportFailure(error, $"Wait for close {node.Name}"); throw; }
    }
}
