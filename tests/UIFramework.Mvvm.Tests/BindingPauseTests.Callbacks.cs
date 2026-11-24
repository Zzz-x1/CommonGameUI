using System.ComponentModel;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed partial class BindingPauseTests
{
    private sealed class OfferSource : IMvvmObject
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Subscribers => PropertyChanged?.GetInvocationList().Length ?? 0;
        public OfferSource? Selected;
        public Action? OnReadSelected;
        public object? Read(string member)
        {
            if (member == "Price") return 7;
            if (member != "Selected") throw new MissingMemberException(member);
            OnReadSelected?.Invoke();
            return Selected;
        }
        public MvvmMember[] Describe() => new[]
        {
            new MvvmMember("Selected", typeof(OfferSource), MvvmMemberKind.Property, true, false),
            new MvvmMember("Price", typeof(int), MvvmMemberKind.Property, true, false)
        };
        public void NotifyChanged(string propertyName) => MvvmNotifications.Raise(PropertyChanged, this, propertyName);
        public void Write(string member, object? value) => throw new NotSupportedException();
        public bool CanExecute(string command) => throw new NotSupportedException();
        public Task Execute(string command, object? parameter, CancellationToken token) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingInPathGetterReleasesEveryObserverDuringSetupOrSourceReplacement(bool replace)
    {
        await using var ui = Context();
        var node = await Node(ui);
        var old = new OfferSource { Selected = new OfferSource() };
        var next = new OfferSource { Selected = new OfferSource() };
        ValueTask closing = default;
        next.OnReadSelected = () => closing = node.DisposeAsync();
        node.SetViewModel("Shop", replace ? old : next);
        int renders = 0;
        using var binding = node.Bind<int>("Shop.Selected.Price", _ => renders++);
        if (replace)
        {
            Assert.Equal(1, old.Subscribers);
            Assert.Equal(1, old.Selected.Subscribers);
            node.SetViewModel("Shop", next);
        }
        await closing;
        Assert.Equal(replace ? 1 : 0, renders);
        Assert.Equal(0, old.Subscribers);
        Assert.Equal(0, old.Selected.Subscribers);
        Assert.Equal(0, next.Subscribers);
        Assert.Equal(0, next.Selected.Subscribers);
    }

    [Fact]
    public async Task CallbackQuoteReadsAllSourcesInOrderAndRefreshesOnceForSourceReplacement()
    {
        await using var ui = Context();
        var node = await Node(ui);
        var buyer = new PlayerVm { Gold = 90 };
        var offer = new PlayerVm { Gold = 12, Amount = 3 };
        node.SetViewModel("Buyer", buyer);
        node.SetViewModel("Offer", offer);
        var quotes = new List<(int Wallet, int Price, int Count)>();
        node.Bind("Buyer.Gold", "Offer.Gold", "Offer.Amount",
            (int wallet, int price, int count) => quotes.Add((wallet, price, count)));
        buyer.Gold = 80;
        offer.Amount = 4;
        node.SetViewModel("Offer", new PlayerVm { Gold = 7, Amount = 2 });
        offer.Gold = 99;
        Assert.Equal(new[] { (90, 12, 3), (80, 12, 3), (80, 12, 4), (80, 7, 2) }, quotes);
    }

    [Fact]
    public async Task CallbackSharedNestedPathsAndAliasedModelsRenderOncePerNotification()
    {
        await using var ui = Context();
        var node = await Node(ui);
        var oldItem = new PlayerVm { Gold = 10, Amount = 2 };
        var owner = new PlayerVm { Selected = oldItem };
        node.SetViewModel("Buyer", owner);
        node.SetViewModel("Shop", owner);
        var values = new List<(int, int)>();
        node.Bind("Buyer.Selected.Gold", "Shop.Selected.Amount", (int gold, int amount) => values.Add((gold, amount)));
        owner.Selected = new PlayerVm { Gold = 8, Amount = 3 };
        oldItem.Gold = 55;
        owner.Selected.Amount = 6;
        Assert.Equal(new[] { (10, 2), (8, 3), (8, 6) }, values);
    }

    [Fact]
    public async Task CallbackPauseMatchesAllInputsAndTracksNestedReplacementsUntilResume()
    {
        await using var ui = Context();
        var node = await Node(ui);
        var buyer = new PlayerVm { Gold = 50, Selected = new PlayerVm { Gold = 6 } };
        node.SetViewModel("Buyer", buyer);
        var target = new Target();
        var label = new Target();
        node.Bind("Buyer.Gold", label);
        var writes = 0;
        var binding = node.Bind("Buyer.Gold", "Buyer.Selected.Gold", target, (int a, int b) => { writes++; return a - b; });
        Assert.Equal(44, target.Value);
        Assert.Throws<InvalidOperationException>(() => node.PauseBinding("Gold"));
        node.PauseBinding("Buyer.Gold");
        Assert.Throws<InvalidOperationException>(() => node.PauseBinding("Buyer.Selected.Gold"));
        buyer.Gold = 30;
        buyer.Selected = new PlayerVm { Gold = 12 };
        buyer.Selected.Gold = 8;
        Assert.Equal(1, writes);
        Assert.Equal(50, label.Value);
        node.ResumeBinding("Buyer.Gold");
        Assert.Equal(2, writes);
        Assert.Equal(22, target.Value);
        Assert.Equal(30, label.Value);
        binding.Dispose();
        buyer.Gold = 12;
        Assert.Equal(2, writes);
        Assert.True(target.Disposed);
        Assert.Throws<KeyNotFoundException>(() => node.PauseBinding("Buyer.Selected.Gold"));
    }

    [Fact]
    public async Task CallbackDuplicatePositionsShareOneNotificationAndPauseGroupEntry()
    {
        await using var ui = Context();
        var node = await Node(ui);
        var model = new PlayerVm { Amount = 2 };
        node.SetViewModel("Cart", model);
        var target = new Target();
        var values = new List<int>();
        node.Bind("Cart.Amount", "Cart.Amount", "Cart.Amount", target, (int a, int b, int c) =>
        {
            var sum = a + b + c;
            values.Add(sum);
            return sum;
        });
        model.Amount = 3;
        node.PauseBinding("Amount");
        model.Amount = 4;
        node.ResumeBinding("Amount");
        await node.DisposeAsync();
        model.Amount = 7;
        Assert.Equal(new[] { 6, 9, 12 }, values);
        Assert.True(target.Disposed);
    }

    [Fact]
    public async Task CallbackConnectionFailureReleasesTargetAndLeavesNoPartialSubscription()
    {
        var host = new Host();
        await using var ui = Context(host);
        var node = await Node(ui);
        var model = new PlayerVm { Gold = 10 };
        node.SetViewModel("Player", model);
        var target = new Target();
        var error = Assert.Throws<InvalidCastException>(() =>
            node.Bind("Player.Gold", "Player.Gold", target, (int gold, string price) => 0));
        Assert.Contains("Player.Gold", error.Message);
        Assert.Contains(error, host.Errors);
        Assert.True(target.Disposed);
        Assert.Throws<InvalidOperationException>(() => node.Bind<int>("Player.SetterCalls", _ => Assert.Fail("unobserved member applied")));
        Assert.Throws<InvalidOperationException>(() => node.Bind<int>("Player.Selected.Gold", _ => Assert.Fail("null intermediate applied")));
        Assert.Throws<KeyNotFoundException>(() => node.PauseBinding("Gold"));
        model.Gold = 11;
        Assert.Equal(0, target.Value);
    }

    [Fact]
    public async Task CallbackErrorPropagatesWithOriginalIdentityAndLaterChangesStillWork()
    {
        var host = new Host();
        await using var ui = Context(host);
        var node = await Node(ui);
        var model = new PlayerVm();
        node.SetViewModel("Player", model);
        var failure = new IOException("Cannot render quote");
        var values = new List<int>();
        node.Bind<int>("Player.Gold", gold =>
        {
            if (gold == 3) throw failure;
            values.Add(gold);
        });
        Assert.Same(failure, Assert.Throws<IOException>(() => model.Gold = 3));
        model.Gold = 4;
        Assert.Equal(new[] { 0, 4 }, values);
        Assert.Contains(failure, host.Errors);
    }
}
