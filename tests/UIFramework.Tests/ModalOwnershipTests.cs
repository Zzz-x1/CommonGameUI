using Xunit;

namespace UIFramework.Tests;

public class ModalOwnershipTests
{
    private sealed class Window : UINode { }
    private sealed class PausableWindow : UINode
    {
        public readonly TaskCompletionSource Exiting = new();
        protected override Task OnExitingAsync(LifecycleContext context, CancellationToken token)
        {
            Exiting.SetResult();
            return Task.Delay(Timeout.Infinite, token);
        }
    }

    private static UIContext Create(out TestHost host)
    {
        host = new TestHost();
        var ui = new UIContext(host, host, host);
        ui.Register(() => new Window(), "window", WindowInstanceMode.Multiple);
        return ui;
    }

    private static async Task<T> Attach<T>(IUINode owner) where T : UINode, new()
    {
        var container = new T();
        await container.AttachAsync(owner, typeof(T).Name);
        return container;
    }

    [Fact] public async Task ShowingRetainedPanelDoesNotDisplaceAnotherVisibleModal()
    {
        await using var ui = Create(out var host);
        var panels = await Attach<PanelContainerNode>(ui.Root);
        var shop = await panels.OpenAsync<Window>();
        var lobby = await panels.OpenAsync<Window>();
        var shopModals = await Attach<ModalContainerNode>(shop);
        var confirm = await shopModals.OpenAsync<Window>();
        var global = await Attach<ModalContainerNode>(ui.Root);
        await panels.HideAsync(shop);
        Assert.False(confirm.IsVisible);
        Assert.Null(ui.InputOwner);
        Assert.True(lobby.IsInteractive);
        var reconnect = await global.OpenAsync<Window>();
        await panels.ShowAsync(shop);
        Assert.Same(reconnect, ui.InputOwner);
        Assert.Same(reconnect.Element, host.Focus);
        Assert.True(confirm.IsVisible);
        Assert.False(confirm.IsInteractive);
        await global.CloseAsync(reconnect);
        Assert.Same(confirm, ui.InputOwner);
        Assert.Same(confirm.Element, host.Focus);
        Assert.True(confirm.IsInteractive);
    }

    [Fact] public async Task ClosingHiddenTopNeverStealsFocusAndRestoresItsPreviousModalOnShow()
    {
        await using var ui = Create(out var host);
        var panels = await Attach<PanelContainerNode>(ui.Root);
        var shop = await panels.OpenAsync<Window>();
        var modals = await Attach<ModalContainerNode>(shop);
        var lower = await modals.OpenAsync<Window>();
        var upper = await modals.OpenAsync<Window>();
        await panels.HideAsync(shop);
        var other = await Attach<ModalContainerNode>(ui.Root);
        var reconnect = await other.OpenAsync<Window>();
        await modals.CloseAsync(upper);
        Assert.Same(reconnect, ui.InputOwner);
        Assert.Same(reconnect.Element, host.Focus);
        await other.CloseAsync(reconnect);
        Assert.Null(ui.InputOwner);
        await panels.ShowAsync(shop);
        Assert.Same(lower, ui.InputOwner);
        Assert.True(lower.IsInteractive);
    }

    [Fact] public async Task StackCoverAndReturnAndTabHideAndSwitchFollowCommittedVisibility()
    {
        await using var ui = Create(out _);
        var pages = await Attach<StackContainerNode>(ui.Root);
        var shop = await pages.PushAsync<Window>();
        var tabs = await Attach<TabContainerNode>(shop);
        var offers = new Window(); var history = new Window();
        await tabs.BindAsync("Offers", offers);
        await tabs.BindAsync("History", history);
        var modals = await Attach<ModalContainerNode>(offers);
        var confirm = await modals.OpenAsync<Window>();
        Assert.Null(ui.InputOwner);
        await tabs.SwitchAsync("Offers"); Assert.Same(confirm, ui.InputOwner);
        await tabs.SwitchAsync("History"); Assert.Null(ui.InputOwner); Assert.True(history.IsInteractive);
        await tabs.SwitchAsync("Offers"); Assert.Same(confirm, ui.InputOwner);
        var details = await pages.PushAsync<Window>();
        Assert.Null(ui.InputOwner); Assert.True(details.IsInteractive);
        await pages.PopAsync(); Assert.Same(confirm, ui.InputOwner);
        await tabs.HideAsync(); Assert.Null(ui.InputOwner);
    }

    [Fact] public async Task CancelingParentHideRetainsModalAndFocus()
    {
        await using var ui = Create(out var host);
        ui.Register(() => new PausableWindow());
        var panels = await Attach<PanelContainerNode>(ui.Root);
        var shop = await panels.OpenAsync<PausableWindow>();
        var modals = await Attach<ModalContainerNode>(shop);
        var confirm = await modals.OpenAsync<Window>();
        using var cancel = new CancellationTokenSource();
        var hide = panels.HideAsync(shop, cancellationToken: cancel.Token);
        await shop.Exiting.Task;
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hide);
        Assert.Same(confirm, ui.InputOwner);
        Assert.True(confirm.IsInteractive);
        Assert.Same(confirm.Element, host.Focus);
    }
}
