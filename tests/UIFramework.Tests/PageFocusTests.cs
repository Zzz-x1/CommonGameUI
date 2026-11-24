using Xunit;

namespace UIFramework.Tests;

public sealed class PageFocusTests
{
    private sealed class Page : UINode, IPreferredFocus
    {
        private MemoryFocus? _button;
        public object PreferredFocus => _button ??= new MemoryFocus(Element!);
    }

    [Fact]
    public async Task NewPageTakesFocusWhenItsPredecessorsModalIsHidden()
    {
        var host = new TestHost();
        await using var ui = new UIContext(host, host, host);
        ui.Register(() => new Page(), "Page", WindowInstanceMode.Multiple);
        ui.Register(() => new CounterView(), "Confirmation");
        var pages = new StackContainerNode();
        await pages.AttachAsync(ui.Root, "Pages");
        var inventory = await pages.PushAsync<Page>();
        var modals = new ModalContainerNode();
        await modals.AttachAsync(inventory, "Sale");
        var confirmation = await modals.OpenAsync<CounterView>();
        var details = await pages.PushAsync<Page>();
        Assert.Null(ui.InputOwner);
        Assert.Same(details.PreferredFocus, host.Focus);
        await pages.PopAsync();
        Assert.Same(confirmation, ui.InputOwner);
        Assert.Same(confirmation.Element, host.Focus);
    }

    [Fact]
    public async Task TabHeaderKeepsFocusAndContentRemembersItsButton()
    {
        var host = new TestHost();
        await using var ui = new UIContext(host, host, host);
        var tabs = new TabContainerNode();
        await tabs.AttachAsync(ui.Root, "Categories");
        var equipment = new Page();
        var materials = new Page();
        await tabs.BindAsync("Equipment", equipment, new MemoryElement());
        await tabs.BindAsync("Materials", materials, new MemoryElement());
        var header = new object();
        host.Focus = header;
        await tabs.SwitchAsync("Equipment");
        Assert.Same(header, host.Focus);
        var row = new MemoryFocus(equipment.Element!);
        host.Focus = row;
        await tabs.SwitchAsync("Materials");
        Assert.Same(materials.PreferredFocus, host.Focus);
        await tabs.SwitchAsync("Equipment");
        Assert.Same(row, host.Focus);
    }

    [Fact]
    public async Task OpeningHudPreservesPageFocusAndCachedReopenUsesPreferredControl()
    {
        var host = new TestHost();
        await using var ui = new UIContext(host, host, host);
        ui.Register(() => new Page(), "Inventory", cacheMode: NodeCacheMode.KeepAlive);
        ui.Register(() => new CounterView(), "Hud");
        var pages = new StackContainerNode();
        await pages.AttachAsync(ui.Root, "Pages");
        var panels = new PanelContainerNode();
        await panels.AttachAsync(ui.Root, "Overlays");
        var page = await pages.PushAsync<Page>();
        var oldRow = new MemoryFocus(page.Element!);
        host.Focus = oldRow;
        await panels.OpenAsync<CounterView>();
        Assert.Same(oldRow, host.Focus);
        await pages.PopAsync();
        Assert.Null(host.Focus);
        Assert.Same(page, await pages.PushAsync<Page>());
        Assert.Same(page.PreferredFocus, host.Focus);
    }
}
