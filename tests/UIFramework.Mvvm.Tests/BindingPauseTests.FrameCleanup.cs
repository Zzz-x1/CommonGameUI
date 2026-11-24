using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed partial class BindingPauseTests
{
    [Theory]
    [InlineData(BindingUpdateTiming.Immediate)]
    [InlineData(BindingUpdateTiming.EndOfFrame)]
    public async Task DisplayFailuresAreReportedOnceAtTheCallerThatRunsTheBinding(BindingUpdateTiming timing)
    {
        var host = new Host();
        await using var ui = Context(host);
        var page = await Node(ui);
        var cart = new PlayerVm();
        page.SetViewModel("Cart", cart);
        var failure = new IOException("Price display");
        Assert.Same(failure, Assert.Throws<IOException>(() => page.Bind<int>("Cart.Gold", _ => throw failure, timing)));
        Assert.Same(failure, Assert.Single(host.Errors));
        host.Errors.Clear();
        page.Bind<int>("Cart.Gold", value => { if (value == 2) throw failure; }, timing);
        if (timing == BindingUpdateTiming.Immediate)
            Assert.Same(failure, Assert.Throws<IOException>(() => cart.Gold = 2));
        else
        {
            cart.Gold = 2;
            Assert.Empty(host.Errors);
            Assert.Same(failure, Assert.Throws<IOException>(ui.GetMvvm().FlushBindings));
        }
        Assert.Same(failure, Assert.Single(host.Errors));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisposingDuringConversionSkipsTheTargetWrite(bool constant)
    {
        await using var ui = Context();
        var node = await Node(ui);
        var target = new Target();
        ValueTask closing = default;
        if (constant)
        {
            node.Bind("", target, new BindingOptions
            {
                HasConstantValue = true, ConstantValue = 3,
                Convert = value => { closing = node.DisposeAsync(); return value; }
            });
        }
        else
        {
            var cart = new PlayerVm { Gold = 2 };
            node.SetViewModel("Cart", cart);
            node.Bind("Cart.Gold", "Cart.Gold", target, (int first, int second) =>
            {
                if (first == 3) closing = node.DisposeAsync();
                return first + second;
            });
            cart.Gold = 3;
            ui.GetMvvm().FlushBindings();
        }
        await closing;
        Assert.True(target.Disposed);
        Assert.Equal(constant ? 0 : 4, target.Value);
    }

    [Fact]
    public async Task DisposingDuringCustomModelSubscriptionReleasesTheJustAddedHandler()
    {
        await using var ui = Context();
        var page = await Node(ui);
        ValueTask closing = default;
        var source = new OfferSource { OnSubscribe = () => closing = page.DisposeAsync() };
        page.SetViewModel("Shop", source);
        page.Bind<int>("Shop.Price", _ => Assert.Fail("Closed shop was rendered"));
        await closing;
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(0, source.PriceReads);
    }

    [Fact]
    public async Task UnknownTimingIsRejectedBeforeTheInitialDisplay()
    {
        var host = new Host();
        await using var ui = Context(host);
        var page = await Node(ui);
        var target = new Target();
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            page.Bind("Shop.Price", target, timing: (BindingUpdateTiming)40));
        Assert.True(target.Disposed);
        Assert.Same(error, Assert.Single(host.Errors));
    }

    [Fact]
    public async Task FrameDriverObservesEveryCompletedTaskBeforeReturningTheFirstError()
    {
        await using var runtime = new MvvmRuntime();
        var driver = new MvvmFrameDriver();
        driver.Attach(runtime);
        var first = new IOException("Order rejected");
        var second = new IOException("Stock unavailable");
        runtime.Observe(Task.FromException(first));
        runtime.Observe(Task.FromException(second));
        var error = Assert.Throws<IOException>(driver.Tick);
        Assert.Same(first, error);
        Assert.Same(second, Assert.Single(UIFailure.GetSecondaryExceptions(error)));
        driver.Tick();
    }
}
