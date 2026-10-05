using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class ResourceTests
{
    private static (UIContext UI, TestHost Host) Create()
    {
        var host = new TestHost(); var ui = new UIContext(host, host, host);
        ui.Register<CounterView>(() => new CounterView(), "window", WindowInstanceMode.Multiple);
        return (ui, host);
    }
    private static async Task<PanelContainerNode> Panels(UIContext ui)
    { var panels = new PanelContainerNode(); await panels.AttachAsync(ui.Root, "Windows"); return panels; }

    [Fact] public async Task PreloadDoesNotConstructNodesOrRunLifecycle()
    {
        var host = new TestHost(); await using var ui = new UIContext(host, host, host);
        var factories = 0;
        ui.Register(() => { factories++; return new CounterView(); }, "window");
        using var lease = await ui.PreloadAsync<CounterView>();
        Assert.Equal(0, factories); Assert.Empty(host.Loaded); Assert.Empty(ui.Root.Children);
        Assert.Single(host.Resources);
    }

    [Fact] public async Task PreloadsAndInstancesShareResourceButHaveIndependentHolds()
    {
        var (ui, host) = Create(); await using var scope = ui;
        var panels = await Panels(ui);
        var a = await ui.PreloadAsync<CounterView>(); var b = await ui.PreloadAsync<CounterView>();
        var first = await panels.OpenAsync<CounterView>(); var second = await panels.OpenAsync<CounterView>();
        var resource = Assert.Single(host.Resources); Assert.Equal(2, host.Loaded.Count);
        a.Dispose(); a.Dispose(); b.Dispose();
        Assert.Equal(0, resource.Disposals); Assert.True(first.IsVisible);
        await panels.CloseAsync(first); Assert.Equal(0, resource.Disposals);
        await panels.CloseAsync(second); Assert.Equal(1, resource.Disposals);
        using var next = await ui.PreloadAsync<CounterView>(); Assert.Equal(2, host.Resources.Count);
    }

    [Fact] public async Task DifferentTypesWithSamePathShareResource()
    {
        var (ui, host) = Create(); await using var scope = ui; ui.Register<ArgumentView>(() => new ArgumentView(), "window");
        using var a = await ui.PreloadAsync<CounterView>(); using var b = await ui.PreloadAsync<ArgumentView>();
        Assert.Single(host.Resources);
    }

    [Fact] public async Task CancelingOneWaiterDoesNotCancelOtherWaiters()
    {
        var (ui, host) = Create(); await using var scope = ui;
        host.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var canceled = new CancellationTokenSource();
        var a = ui.PreloadAsync<CounterView>(canceled.Token); var b = ui.PreloadAsync<CounterView>();
        canceled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => a);
        Assert.False(b.IsCompleted); host.LoadGate.SetResult(true);
        using var lease = await b; Assert.Single(host.Resources); Assert.Equal(0, host.Resources[0].Disposals);
    }

    [Fact] public async Task CanceledLastWaiterReleasesLateResource()
    {
        var (ui, host) = Create();
        host.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        host.ResourceCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var pending = ui.PreloadAsync<CounterView>(cancellation.Token);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        host.LoadGate.SetResult(true);
        await host.ResourceCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await ui.DisposeAsync();
        Assert.Empty(host.Loaded); Assert.Equal(1, Assert.Single(host.Resources).Disposals);
    }

    [Fact] public async Task ContextDisposesOutstandingPreloadsAndCancelsPendingLoad()
    {
        var (ui, host) = Create(); var lease = await ui.PreloadAsync<CounterView>();
        ui.Register<ArgumentView>(() => new ArgumentView(), "other"); host.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = ui.PreloadAsync<ArgumentView>();
        await ui.DisposeAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, Assert.Single(host.Resources).Disposals); lease.Dispose(); await ui.DisposeAsync();
        Assert.Equal(1, host.Resources[0].Disposals);
    }

    [Fact] public async Task LoadFailureIsOriginalSharedAndNotCachedForNextRequest()
    {
        var (ui, host) = Create(); await using var scope = ui;
        host.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new IOException("resource failed"); host.LoadFailure = error;
        var a = ui.PreloadAsync<CounterView>(); var b = ui.PreloadAsync<CounterView>();
        host.LoadGate.SetResult(true);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => a));
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => b)); Assert.Single(host.Errors);
        host.LoadFailure = null; using var retry = await ui.PreloadAsync<CounterView>(); Assert.Single(host.Resources);
    }

    [Fact] public async Task InstantiateFailureKeepsOriginalAndReleasesReservation()
    {
        var (ui, host) = Create(); await using var scope = ui; var panels = await Panels(ui);
        var lease = await ui.PreloadAsync<CounterView>(); var resource = Assert.Single(host.Resources);
        var error = new IOException("instantiate failed"); resource.InstantiateFailure = error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => panels.OpenAsync<CounterView>()));
        Assert.Empty(panels.Children); Assert.Equal(0, resource.Disposals); lease.Dispose(); Assert.Equal(1, resource.Disposals);
    }

    [Fact] public async Task ElementAndResourceCleanupFailuresPreserveFirstException()
    {
        var (ui, host) = Create(); await using var scope = ui; var panels = await Panels(ui);
        var node = await panels.OpenAsync<CounterView>();
        var first = new IOException("element cleanup"); var second = new IOException("resource cleanup");
        host.Loaded[0].DisposeFailure = first; host.Resources[0].DisposeFailure = second;
        Assert.Same(first, await Assert.ThrowsAsync<IOException>(() => panels.CloseAsync(node)));
        Assert.Contains(second, UIFailure.GetSecondaryExceptions(first)); Assert.Contains(second, host.Errors);
        Assert.Equal(NodeState.Disposed, node.State); Assert.Equal(1, host.Resources[0].Disposals);
    }

    [Fact] public async Task PreloadingUnregisteredOrResourcelessTypeFailsExplicitly()
    {
        var (ui, _) = Create(); await using var scope = ui;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => ui.PreloadAsync<ArgumentView>());
        ui.Register<ArgumentView>(() => new ArgumentView()); await Assert.ThrowsAsync<InvalidOperationException>(() => ui.PreloadAsync<ArgumentView>());
        await ui.DisposeAsync(); await Assert.ThrowsAsync<ObjectDisposedException>(() => ui.PreloadAsync<CounterView>());
    }

    [Fact] public async Task OrphanedLoadFailureIsLoggedAndPropagatedByScopeDisposal()
    {
        var (ui, host) = Create(); host.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var pending = ui.PreloadAsync<CounterView>(cancellation.Token);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var error = new IOException("orphaned background failure"); host.LoadFailure = error;
        host.ErrorReported = new(TaskCreationOptions.RunContinuationsAsynchronously);
        host.LoadGate.SetResult(true);
        // The backend must reach its failure before disposal requests cancellation.
        Assert.Same(error, await host.ErrorReported.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(async () => await ui.DisposeAsync()));
        Assert.Single(host.Errors, e => ReferenceEquals(e, error));
    }
    [Fact] public async Task ResourceReleaseSharesContextDisposalTask()
    {
        var (ui, host) = Create();
        var lease = await ui.PreloadAsync<CounterView>();
        var resource = Assert.Single(host.Resources);
        var original = new IOException("Resource release failed.");
        resource.DisposeFailure = original;
        Task? repeated = null;
        resource.OnDispose = () => repeated = ui.DisposeAsync().AsTask();

        var disposal = ui.DisposeAsync().AsTask();
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => disposal));
        Assert.Same(disposal, repeated);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => repeated!));
        Assert.Equal(1, resource.Disposals);
        lease.Dispose();
    }

}
