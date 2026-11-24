using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public sealed class StackLifetimeReturnTests
{
    private sealed class Lobby : CounterView
    {
        internal LifecycleReason Activation;
        internal bool WaitOnReturn;
        internal int CompletedEnters;
        protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token) =>
            WaitOnReturn && context.Reason == LifecycleReason.Return
                ? Task.Delay(Timeout.Infinite, token)
                : base.OnEnteringAsync(context, token);
        protected override void OnEntered(LifecycleContext context) => CompletedEnters++;
        protected override void OnActivated(StateChangeContext context)
        {
            base.OnActivated(context);
            Activation = context.Operation.Reason;
        }
    }
    private sealed class Details : CounterView { }
    private sealed class Shop : CounterView { }

    private static async Task<(UIContext, StackContainerNode, TestHost)> Create()
    {
        var host = new TestHost();
        var ui = new UIContext(host, host, host);
        ui.Register(() => new Lobby(), "Lobby");
        ui.Register(() => new Details(), "Details", cacheMode: NodeCacheMode.KeepAlive);
        ui.Register(() => new Shop(), "Shop");
        var stack = new StackContainerNode();
        await stack.AttachAsync(ui.Root, "Pages", new MemoryElement());
        return (ui, stack, host);
    }

    [Fact]
    public async Task LifetimeEndingReturnsToExistingLobbyBeforeCloseWaiterCompletes()
    {
        var (ui, pages, _) = await Create(); await using var cleanup = ui;
        var lobby = await pages.PushAsync<Lobby>();
        var details = await pages.PushAsync<Details>();
        var closed = details.WaitForCloseAsync();
        var disposing = details.DisposeAsync();
        await closed;
        Assert.Same(lobby, Assert.Single(pages.History));
        Assert.Same(lobby, pages.Current);
        Assert.True(lobby.IsVisible && lobby.IsInteractive);
        Assert.True(((MemoryElement)lobby.Element!).Visible);
        Assert.Equal(NodeState.Disposed, details.State);
        Assert.Same(lobby, await pages.PushAsync<Lobby>());
        Assert.Equal(1, lobby.Entered);
        Assert.Equal(1, lobby.CompletedEnters);
        Assert.Equal(2, lobby.Activated);
        Assert.Equal(LifecycleReason.Return, lobby.Activation);
        await disposing;
    }

    [Fact]
    public async Task RestoredPageRegainsItsModalInputAndFocus()
    {
        var (ui, pages, host) = await Create(); await using var cleanup = ui;
        var lobby = await pages.PushAsync<Lobby>();
        var modals = new ModalContainerNode();
        await modals.AttachAsync(lobby, "LobbyDialogs");
        var offer = await modals.OpenAsync<Shop>();
        var details = await pages.PushAsync<Details>();
        Assert.Null(ui.InputOwner);
        await details.DisposeAsync();
        Assert.Same(lobby, pages.Current);
        Assert.Same(offer, ui.InputOwner);
        Assert.True(offer.IsInteractive);
        Assert.Same(offer.Element, host.Focus);
    }

    [Fact]
    public async Task RemovingMiddlePageAndDisposingContextDoNotActivateCoveredPages()
    {
        var (ui, pages, _) = await Create(); await using var cleanup = ui;
        var lobby = await pages.PushAsync<Lobby>();
        var details = await pages.PushAsync<Details>();
        var shop = await pages.PushAsync<Shop>();
        await details.DisposeAsync();
        Assert.Same(shop, pages.Current);
        Assert.True(shop.IsInteractive);
        Assert.False(lobby.IsVisible);
        await ui.DisposeAsync();
        Assert.Equal(1, lobby.Activated);
        Assert.Equal(NodeState.Disposed, lobby.State);
    }

    [Fact]
    public async Task PendingPushCoversRestoredPageIfTopDisappearsWhileLoading()
    {
        var (ui, pages, host) = await Create(); await using var cleanup = ui;
        var lobby = await pages.PushAsync<Lobby>();
        var details = await pages.PushAsync<Details>();
        host.LoadGate = new();
        var loading = pages.PushAsync<Shop>();
        await details.DisposeAsync();
        Assert.Same(lobby, pages.Current);
        Assert.True(lobby.IsVisible);
        Assert.False(lobby.IsInteractive); // Pending push still owns the input lock.
        host.LoadGate.SetResult(true);
        var shop = await loading;
        Assert.Same(shop, pages.Current);
        Assert.False(lobby.IsVisible);
        await pages.PopAsync();
        Assert.True(lobby.IsInteractive);
    }

    [Fact]
    public async Task LosingOutgoingPageCancelsPendingReturnPreparationAndRestoresSurvivor()
    {
        var (ui, pages, _) = await Create(); await using var cleanup = ui;
        var lobby = await pages.PushAsync<Lobby>();
        var details = await pages.PushAsync<Details>();
        lobby.WaitOnReturn = true;
        var returning = pages.PopAsync();
        Assert.False(returning.IsCompleted);
        await details.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => returning);
        Assert.Same(lobby, pages.Current);
        Assert.True(lobby.IsVisible && lobby.IsInteractive);
        Assert.Equal(2, lobby.Activated);
    }
}
