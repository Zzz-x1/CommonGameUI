using Xunit;

namespace UIFramework.Tests;

public class ModalFocusChainTests
{
    private sealed class DelayedDialog : UINode { }

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
}
