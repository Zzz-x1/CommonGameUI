using System.Runtime.ExceptionServices;

namespace UIFramework;

internal static class TransitionFlow
{
    internal static async Task PrepareAsync(ContainerNode container, LifecycleContext? exit,
        LifecycleContext? enter, TransitionDirection direction, bool playAnimation, CancellationToken token)
    {
        var context = container.Context;
        // Freeze recipes before business preparation; execution state is created afterwards.
        var exitContext = exit == null ? null : new TransitionContext(exit, false, direction);
        var enterContext = enter == null ? null : new TransitionContext(enter, true, direction);
        IUITransition Resolve(TransitionContext item) => context.Query(
            () => Transitions.Resolve(item.Node.Transition ?? container.ChildTransition ?? context.DefaultTransition, item),
            $"Select transition {item.Node.GetType().Name}/{item.Node.Name} ({item.Node.Id}) {item.Phase}");
        var runs = new List<IUITransitionRun>();
        var orders = new List<(IUIElement Element, object Order)>();
        var restoreCount = 0;
        void Restore(Exception? primary)
        {
            Exception? failure = primary;
            void Attempt(Action action)
            {
                try { action(); }
                catch (Exception ex)
                {
                    context.Report(ex, "Restore transition presentation");
                    if (failure == null) failure = ex; else if (!ReferenceEquals(ex, failure)) UIFailure.AddSecondary(failure, ex);
                }
            }
            while (restoreCount < runs.Count) Attempt(runs[restoreCount++].Restore);
            foreach (var order in orders) Attempt(() => order.Element.RestoreOrder(order.Order));
            orders.Clear();
            if (primary == null && failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        try
        {
            var exitRecipe = playAnimation && exitContext != null ? Resolve(exitContext) : null;
            var enterRecipe = playAnimation && enterContext != null ? Resolve(enterContext) : null;
            if (exit != null) await exit.Node.PrepareExitAsync(exit, token);
            if (enter != null) await enter.Node.PrepareEnterAsync(enter, token);
            token.ThrowIfCancellationRequested();
            if (!playAnimation)
            {
                enter?.Node.StageVisibility(true);
                exit?.Node.StageVisibility(false);
                return;
            }
            foreach (var node in new[] { exit?.Node, enter?.Node })
                if (node?.Element is { } element) orders.Add((element, element.CaptureOrder()));
            if (exit != null) runs.Add(exitRecipe!.Create(exitContext!)
                ?? throw new InvalidOperationException("Transition factory returned null."));
            if (enter != null)
            {
                runs.Add(enterRecipe!.Create(enterContext!)
                    ?? throw new InvalidOperationException("Transition factory returned null."));
                enter.Node.StageVisibility(true);
            }
            if (direction == TransitionDirection.Backward) exit?.Node.Element?.BringToFront();
            else enter?.Node.Element?.BringToFront();

            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
            Exception? first = null;
            async Task Play(IUITransitionRun run)
            {
                try { await run.PlayAsync(cancel.Token); }
                catch (Exception ex)
                {
                    context.Report(ex, "Play transition");
                    if (Interlocked.CompareExchange(ref first, ex, null) == null)
                    {
                        await context.CleanupAsync(ex, () => { cancel.Cancel(); return Task.CompletedTask; });
                    }
                    else if (!ReferenceEquals(first, ex)) { lock (first!) UIFailure.AddSecondary(first!, ex); }
                }
            }
            // Play observes synchronous throws as well as every asynchronous failure.
            await Task.WhenAll(runs.Select(Play));
            if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
            token.ThrowIfCancellationRequested();
            exit?.Node.StageVisibility(false);
            Restore(null);
        }
        catch (Exception ex)
        {
            Restore(ex);
            throw;
        }
    }
}
