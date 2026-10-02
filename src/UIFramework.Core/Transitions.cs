namespace UIFramework;

/// <summary>Wraps synchronous business selection as a reusable transition recipe.</summary>
public static class Transitions
{
    public static IUITransition Select(Func<TransitionContext, IUITransition> select)
    {
        try { return new SelectedTransition(select ?? throw new ArgumentNullException(nameof(select))); }
        catch (Exception ex) { new ConsoleUILog().Error(ex, "Configure transition selector"); throw; }
    }

    internal static IUITransition Resolve(IUITransition recipe, TransitionContext context)
    {
        var visited = new HashSet<SelectedTransition>();
        while (recipe is SelectedTransition selected)
        {
            if (!visited.Add(selected)) throw new InvalidOperationException("Transition selection contains a cycle.");
            recipe = selected.Select(context) ?? throw new InvalidOperationException("Transition selector returned null.");
        }
        return recipe;
    }

    private sealed class SelectedTransition(Func<TransitionContext, IUITransition> select) : IUITransition
    {
        internal IUITransition Select(TransitionContext context) => select(context);
        public IUITransitionRun Create(TransitionContext context) => context.Node.Context.Query(
            () => Resolve(this, context).Create(context), $"Create selected transition {context.Node.Name} ({context.Node.Id})");
    }
}
