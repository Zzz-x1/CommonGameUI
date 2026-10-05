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
        var items = new[] { exitContext, enterContext }.Where(item => item != null).Cast<TransitionContext>().ToArray();
        var sourceOrder = exit?.Node.TransitionOrder ?? 0;
        var targetOrder = enter?.Node.TransitionOrder ?? 0;
        IUITransition Resolve(TransitionContext item) => context.Query(
            () => Transitions.Resolve(item.Node.Transition ?? container.ChildTransition ?? context.DefaultTransition, item),
            $"Select transition {item.Node.GetType().Name}/{item.Node.Name} ({item.Node.Id}) {item.Phase}");
        var runs = new List<IUITransitionRun>();
        var orders = new List<(IUIElement Element, object Order)>();
        var restoreCount = 0;
        void Finish(TransitionStage stage, Exception? primary)
        {
            Exception? failure = primary;
            foreach (var item in items)
            {
                item.SetProgressReporter(null);
                try
                {
                    item.Node.Node.PublishTransition(item, stage, stage == TransitionStage.Completed ? 1 : item.Node.TransitionProgress);
                }
                catch (Exception ex)
                {
                    context.Report(ex, "Finish transition notification");
                    if (failure == null) failure = ex; else if (!ReferenceEquals(ex, failure)) UIFailure.AddSecondary(failure, ex);
                }
            }
            if (primary == null && failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
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
            foreach (var item in items)
            {
                item.SetProgressReporter(null);
                item.Node.Node.PublishTransition(item, TransitionStage.Preparing, 0);
            }
            var exitRecipe = playAnimation && exitContext != null ? Resolve(exitContext) : null;
            var enterRecipe = playAnimation && enterContext != null ? Resolve(enterContext) : null;
            if (exit != null) await exit.Node.Node.PrepareExitAsync(exit, token);
            if (enter != null) await enter.Node.Node.PrepareEnterAsync(enter, token);
            token.ThrowIfCancellationRequested();
            if (!playAnimation)
            {
                enter?.Node.Node.StageVisibility(true);
                exit?.Node.Node.StageVisibility(false);
                Finish(TransitionStage.Completed, null);
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
                enter.Node.Node.StageVisibility(true);
            }
            if (exit != null && enter != null && sourceOrder != targetOrder)
            {
                if (sourceOrder > targetOrder) exit.Node.Element?.BringToFront();
                else enter.Node.Element?.BringToFront();
            }
            else if (direction == TransitionDirection.Backward) exit?.Node.Element?.BringToFront();
            else enter?.Node.Element?.BringToFront();

            foreach (var item in items)
            {
                item.SetProgressReporter(progress =>
                {
                    if (item.Node.TransitionState?.Stage != TransitionStage.Playing)
                        throw new InvalidOperationException("Progress can only be reported during playback.");
                    if (progress < item.Node.TransitionProgress)
                        throw new InvalidOperationException("Transition progress cannot move backwards.");
                    if (progress != item.Node.TransitionProgress) item.Node.Node.PublishTransition(item, TransitionStage.Playing, progress);
                });
                item.Node.Node.PublishTransition(item, TransitionStage.Playing, 0);
            }

            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(items.Select(item => item.Node.Lifetime).Append(token).ToArray());
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
            cancel.Token.ThrowIfCancellationRequested();
            exit?.Node.Node.StageVisibility(false);
            Restore(null);
            Finish(TransitionStage.Completed, null);
        }
        catch (Exception ex)
        {
            Restore(ex);
            Finish(ex is OperationCanceledException ? TransitionStage.Canceled : TransitionStage.Failed, ex);
            throw;
        }
    }
}
