using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed partial class BindingPauseTests
{
    [Fact]
    public async Task QuoteReadsEachFinalSourceOnceEvenWhenAPathIsRepeated()
    {
        await using var ui = Context();
        var node = await Node(ui);
        var offer = new OfferSource { Price = 9 };
        var cart = new OfferSource { Price = 1 };
        node.SetViewModel("Offer", offer);
        node.SetViewModel("Cart", cart);
        var displayed = new List<(int, int, int)>();
        node.Bind("Offer.Price", "Cart.Price", "Offer.Price",
            (int price, int quantity, int duplicate) => displayed.Add((price, quantity, duplicate)));
        Assert.Equal(new[] { (9, 1, 9) }, displayed);
        offer.PriceReads = cart.PriceReads = 0;
        cart.Price = 2; cart.NotifyChanged("Price");
        cart.Price = 3; cart.NotifyChanged("Price");
        offer.Price = 8; offer.NotifyChanged("Price");
        Assert.Equal((0, 0), (offer.PriceReads, cart.PriceReads));
        Assert.Single(displayed);
        ui.GetMvvm().FlushBindings();
        ui.GetMvvm().FlushBindings();
        Assert.Equal((1, 1), (offer.PriceReads, cart.PriceReads));
        Assert.Equal(new[] { (9, 1, 9), (8, 3, 8) }, displayed);
    }

    [Fact]
    public async Task SourceReplacementDisconnectsOldChildrenAndResolvesOnlyTheFinalBranch()
    {
        await using var ui = Context();
        var node = await Node(ui);
        var old = new OfferSource { Selected = new OfferSource() };
        var transient = new OfferSource { Selected = new OfferSource(), OnReadSelected = () => Assert.Fail("Obsolete branch was read") };
        var latest = new OfferSource { Selected = new OfferSource { Price = 11 } };
        node.SetViewModel("Shop", old);
        var values = new List<int>();
        node.Bind<int>("Shop.Selected.Price", values.Add);
        node.SetViewModel("Shop", transient);
        Assert.Equal(0, old.Selected.Subscribers);
        node.SetViewModel("Shop", latest);
        latest.Selected.Price = 12; latest.Selected.NotifyChanged("Price");
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { 7, 12 }, values);
        Assert.Equal(1, latest.Selected.PriceReads);
        Assert.Equal(1, latest.Selected.Subscribers);
        latest.Selected.NotifyChanged("Price");
        ui.GetMvvm().FlushBindings();
        Assert.Equal(3, values.Count);
    }

    [Fact]
    public async Task NotificationsDuringFlushUpdatePendingBindingsAndScheduleOtherWorkForNextTick()
    {
        await using var ui = Context();
        var node = await Node(ui);
        var vm = new PlayerVm();
        node.SetViewModel("Cart", vm);
        var renders = new List<string>();
        node.Bind<int>("Cart.Gold", gold =>
        {
            renders.Add($"gold {gold}");
            if (gold != 1) return;
            vm.Gold = 2;              // Currently rendering
            vm.Amount = 3;            // Already waiting in this batch
            vm.Selected!.Gold = 4;    // First notification in this batch
        });
        vm.Selected = new PlayerVm();
        node.Bind<int>("Cart.Amount", value => renders.Add($"amount {value}"));
        node.Bind<int>("Cart.Selected.Gold", value => renders.Add($"selection {value}"));
        renders.Clear();
        vm.Gold = 1;
        vm.Amount = 1;
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { "gold 1", "amount 3" }, renders);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { "gold 1", "amount 3", "gold 2", "selection 4" }, renders);
    }

    [Fact]
    public async Task FlushingAllBindingsPropagatesTheFirstOriginalFailureAfterIndependentWork()
    {
        var host = new Host();
        await using var ui = Context(host);
        var node = await Node(ui);
        var vm = new PlayerVm();
        node.SetViewModel("Cart", vm);
        var readError = new IOException("Price format");
        var applyError = new FormatException("Quote");
        node.Bind("Cart.Gold", new Target(), new BindingOptions { Convert = value => (int)value! == 1 ? throw readError : value });
        node.Bind<int>("Cart.Gold", gold => { if (gold == 1) throw applyError; });
        var label = new Target();
        node.Bind("Cart.Gold", label);
        vm.Gold = 1;
        Assert.Empty(host.Errors);
        var error = Assert.Throws<IOException>(ui.GetMvvm().FlushBindings);
        Assert.Same(readError, error);
        Assert.Contains(applyError, UIFailure.GetSecondaryExceptions(error));
        Assert.Contains(readError, host.Errors);
        Assert.Contains(applyError, host.Errors);
        Assert.Equal(1, label.Value);
        ui.GetMvvm().FlushBindings();
        vm.Gold = 2;
        ui.GetMvvm().FlushBindings();
        Assert.Equal(2, label.Value);
    }

    [Fact]
    public async Task PauseResumeAndDisposalConsumePendingWorkAcrossSharedContexts()
    {
        await using var first = Context();
        await using var second = Context();
        second.ShareMvvm(first);
        var driver = new MvvmFrameDriver();
        driver.Attach(first.GetMvvm());
        driver.Attach(second.GetMvvm());
        var vm = new PlayerVm();
        var a = await Node(first);
        var b = await Node(second);
        a.SetViewModel("Cart", vm); b.SetViewModel("Cart", vm);
        var left = new List<int>(); var right = new List<int>();
        a.Bind<int>("Cart.Gold", left.Add);
        b.Bind<int>("Cart.Gold", right.Add);
        vm.Gold = 1;
        a.PauseBinding("Gold");
        vm.Gold = 2;
        a.ResumeBinding("Gold");
        await b.DisposeAsync();
        driver.Tick();
        Assert.Equal(new[] { 0, 2 }, left);
        Assert.Equal(new[] { 0 }, right);
        vm.Gold = 3;
        await second.DisposeAsync();
        driver.Tick();
        Assert.Equal(new[] { 0, 2, 3 }, left);
        Assert.False(driver.IsEmpty);
        await first.DisposeAsync();
        driver.Tick();
        Assert.True(driver.IsEmpty);
    }

    [Fact]
    public async Task FrameDriverAdvancesEachAttachedRuntimeOnceEvenWhenAnotherRuntimeFails()
    {
        await using var first = Context();
        await using var other = Context();
        var driver = new MvvmFrameDriver();
        driver.Attach(first.GetMvvm());
        driver.Attach(first.GetMvvm());
        driver.Attach(other.GetMvvm());
        var vm = new PlayerVm();
        var a = await Node(first);
        var b = await Node(other);
        a.SetViewModel("Cart", vm);
        b.SetViewModel("Cart", vm);
        var renders = new List<int>();
        var actual = new IOException("Display failed");
        a.Bind<int>("Cart.Gold", value =>
        {
            renders.Add(value);
            if (value != 1) return;
            vm.Gold = 2;
            throw actual;
        });
        var label = new Target();
        b.Bind("Cart.Gold", label);
        vm.Gold = 1;
        Assert.Same(actual, Assert.Throws<IOException>(driver.Tick));
        Assert.Equal(2, label.Value);
        Assert.Equal(new[] { 0, 1 }, renders);
        driver.Tick();
        Assert.Equal(new[] { 0, 1, 2 }, renders);
    }

    [Fact]
    public async Task CachedWindowsKeepPendingBindingsUntilEvicted()
    {
        await using var ui = Context();
        ui.Register(() => new UINode(), cacheMode: NodeCacheMode.KeepAlive);
        var windows = new PanelContainerNode();
        await windows.AttachAsync(ui.Root, "Windows");
        var shop = await windows.OpenAsync<UINode>();
        var cart = new PlayerVm();
        shop.SetViewModel("Cart", cart);
        var values = new List<int>();
        shop.Bind<int>("Cart.Amount", values.Add);
        cart.Amount = 2;
        await windows.CloseAsync(shop);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { 0, 2 }, values);
        cart.Amount = 3;
        await windows.ClearCacheAsync();
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { 0, 2 }, values);
    }
}
