using Xunit;

namespace UIFramework.Tests;

public class ModalFocusChainTests
{
    private sealed class DelayedDialog : UINode { }
    private sealed class Inventory : UINode, IPreferredFocus
    {
        public object PreferredFocus => Element!;
    }

    private static UIContext Create(out TestHost host)
    {
        host = new TestHost();
        var ui = new UIContext(host, host, host);
        ui.Register(() => new CounterView(), "window", WindowInstanceMode.Multiple);
        ui.Register(() => new DelayedDialog(), "delayed");
        return ui;
    }

    private static async Task<T> Attach<T>(UIContext ui) where T : ContainerNode, new()
    {
        var container = new T();
        await container.AttachAsync(ui.Root, typeof(T).Name);
        return container;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClosingLastDialogRestoresOriginalOpenerAfterCoveredDialogsEnd(bool oldestFirst)
    {
        await using var ui = Create(out var host);
        var pages = await Attach<StackContainerNode>(ui);
        var lobby = await pages.PushAsync<CounterView>();
        var opener = new MemoryFocus(lobby.Element!);
        host.Focus = opener;
        var modals = await Attach<ModalContainerNode>(ui);
        var first = await modals.OpenAsync<CounterView>();
        var second = await modals.OpenAsync<CounterView>();
        var top = await modals.OpenAsync<CounterView>();
        await (oldestFirst ? first : second).DisposeAsync();
        await (oldestFirst ? second : first).DisposeAsync();
        Assert.Same(top, ui.InputOwner);
        Assert.Same(top.Element, host.Focus);
        await modals.CloseAsync(top);
        Assert.Same(opener, host.Focus);
    }

    [Fact]
    public async Task ExpiringDialogDoesNotReplaceAnUnrelatedFocusBookmark()
    {
        await using var ui = Create(out var host);
        var modals = await Attach<ModalContainerNode>(ui);
        var first = await modals.OpenAsync<CounterView>();
        var other = new object();
        host.Focus = other;
        var top = await modals.OpenAsync<CounterView>();
        await first.DisposeAsync();
        await modals.CloseAsync(top);
        Assert.Same(other, host.Focus);
    }

    [Fact]
    public async Task LoadingDialogKeepsBackgroundBookmarkIfItsOpenerExpiresDuringLoad()
    {
        await using var ui = Create(out var host);
        var pages = await Attach<StackContainerNode>(ui);
        var lobby = await pages.PushAsync<CounterView>();
        var opener = new MemoryFocus(lobby.Element!);
        host.Focus = opener;
        var modals = await Attach<ModalContainerNode>(ui);
        var first = await modals.OpenAsync<CounterView>();
        host.LoadGate = new TaskCompletionSource<bool>();
        var opening = modals.OpenAsync<DelayedDialog>();
        Assert.False(opening.IsCompleted);
        await first.DisposeAsync();
        host.LoadGate.SetResult(true);
        var top = await opening;
        await modals.CloseAsync(top);
        Assert.Same(opener, host.Focus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReopeningCachedParentInvalidatesFocusFromItsRetainedTabOrInnerStack(bool useTab)
    {
        await using var ui = Create(out var host);
        ui.Register(() => new Inventory(), "inventory", cacheMode: NodeCacheMode.KeepAlive);
        var panels = await Attach<PanelContainerNode>(ui);
        var inventory = await panels.OpenAsync<Inventory>();
        UINode content;
        if (useTab)
        {
            var tabs = new TabContainerNode();
            await tabs.AttachAsync(inventory, "Categories");
            content = new UINode();
            await tabs.BindAsync("Equipment", content, new MemoryElement());
            await tabs.SwitchAsync("Equipment");
        }
        else
        {
            var pages = new StackContainerNode();
            await pages.AttachAsync(inventory, "DetailPages");
            content = await pages.PushAsync<CounterView>();
        }
        host.Focus = new MemoryFocus(content.Element!);
        var modals = await Attach<ModalContainerNode>(ui);
        var dialog = await modals.OpenAsync<CounterView>();
        await panels.CloseAsync(inventory);
        Assert.Same(inventory, await panels.OpenAsync<Inventory>());
        await modals.CloseAsync(dialog);
        Assert.Same(inventory.PreferredFocus, host.Focus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CanceledPushFinishesForcedReturnsFocusAfterUnlockWithoutTakingHudFocus(bool focusWasInPage)
    {
        await using var ui = Create(out var host);
        var pages = await Attach<StackContainerNode>(ui);
        var lobby = await pages.PushAsync<CounterView>();
        var lobbySelection = new MemoryFocus(lobby.Element!);
        host.Focus = lobbySelection;
        var details = await pages.PushAsync<CounterView>();
        var hud = new object();
        host.Focus = focusWasInPage ? new MemoryFocus(details.Element!) : hud;
        host.LoadGate = new TaskCompletionSource<bool>();
        using var cancel = new CancellationTokenSource();
        var loading = pages.PushAsync<DelayedDialog>(cancellationToken: cancel.Token);
        await details.DisposeAsync();
        Assert.False(lobby.IsInteractive);
        host.Focus = null; // Native controls may surrender selection while their container is locked.
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
        Assert.True(lobby.IsInteractive);
        Assert.Same(focusWasInPage ? lobbySelection : hud, host.Focus);
    }
}
