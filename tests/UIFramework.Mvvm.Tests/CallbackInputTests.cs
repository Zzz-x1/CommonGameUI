using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed class CallbackInputTests
{
    private sealed class Host : IUIHost, IUILog, IUIResourceLoader
    {
        public readonly List<Exception> Errors = new();
        public void VerifyAccess() { }
        public object? CaptureFocus() => null;
        public bool ContainsFocus(IUIElement scope, object target) => false;
        public bool TryFocus(IUIElement? scope, object? target) => false;
        public void ClearFocus() { }
        public void Error(string message) => throw new InvalidOperationException(message);
        public void Exception(Exception exception, string context) => Errors.Add(exception);
        public Task<IUIResource> LoadAsync(string path, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class Picker<T>
    {
        private Action<T>? _edited;
        public T Value = default!;
        public readonly List<T> Displayed = new();
        public int Connections, Disconnections;
        public Action<T>? LatestListener;
        public Action Connect(Action<T> edited)
        {
            Connections++;
            _edited += edited;
            LatestListener = edited;
            // Some controls publish their current draft immediately when an observer is connected.
            edited(Value);
            return () => { _edited -= edited; Disconnections++; };
        }
        public void Display(T value)
        {
            Value = value;
            Displayed.Add(value);
            _edited?.Invoke(value);
        }
        public void Edit(T value) { Value = value; _edited?.Invoke(value); }
    }

    [Fact]
    public async Task QuantityUsesVmInitialValueAndEchoesOnlyTheAcceptedEdit()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Shop");
        var cart = new PlayerVm { Amount = 5 };
        page.SetViewModel("Cart", cart);
        var picker = new Picker<int> { Value = 80 };
        var binding = page.BindTwoWay<int>("Cart.Amount", picker.Display, picker.Connect);
        Assert.Equal(new[] { 5 }, picker.Displayed);
        Assert.Equal(1, cart.SetterCalls);
        picker.Edit(120);
        Assert.Equal(100, cart.Amount);
        Assert.Equal(new[] { 5 }, picker.Displayed);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { 5, 100 }, picker.Displayed);
        Assert.Equal(2, cart.SetterCalls);
        picker.Edit(120);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { 5, 100, 100 }, picker.Displayed);
        Assert.Equal(3, cart.SetterCalls);

        page.PauseBinding("Amount");
        cart.Amount = 3;
        picker.Edit(22);
        Assert.Equal(3, cart.Amount);
        page.ResumeBinding("Cart.Amount");
        Assert.Equal(3, picker.Value);
        var replacement = new PlayerVm { Amount = 17 };
        page.SetViewModel("Cart", replacement);
        picker.Edit(7);
        Assert.Equal(3, cart.Amount);
        Assert.Equal(7, replacement.Amount);

        binding.Dispose();
        picker.LatestListener!(44);
        await page.DisposeAsync();
        Assert.Equal(7, replacement.Amount);
        Assert.Equal(1, picker.Connections);
        Assert.Equal(1, picker.Disconnections);
    }

    [Fact]
    public async Task PriceRangeCommitsToTwoModelsAndPaintsOnceAfterBothNotifications()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Filter");
        var left = new PlayerVm { Gold = 1 };
        var right = new PlayerVm { Gold = 8 };
        page.SetViewModel("Low", left);
        page.SetViewModel("High", right);
        var observed = new List<(int, int)>();
        page.Bind("Low.Gold", "High.Gold", (int a, int b) => observed.Add((a, b)), timing: BindingUpdateTiming.Immediate);
        var picker = new Picker<(int Low, int High)>();
        page.BindTwoWay<int, int, (int Low, int High)>("Low.Gold", "High.Gold",
            (a, b) => picker.Display((a, b)), picker.Connect, edit =>
            {
                left.Gold = edit.Low;
                right.Gold = edit.High;
            });
        picker.Edit((3, 9));
        Assert.Single(picker.Displayed);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { (1, 8), (3, 9) }, picker.Displayed);
        Assert.Equal(new[] { (1, 8), (3, 8), (3, 9) }, observed);
        page.PauseBinding("High.Gold");
        left.Gold = 2;
        picker.Edit((0, 10));
        Assert.Equal(2, left.Gold);
        Assert.Equal(9, right.Gold);
        page.ResumeBinding("High.Gold");
        Assert.Equal((2, 9), picker.Value);
        await page.DisposeAsync();
        picker.Edit((11, 12));
        Assert.Equal(2, left.Gold);
        Assert.Equal(1, picker.Disconnections);
    }

    [Fact]
    public async Task ExplicitCommitKeepsPartialChangesAndRecoversAfterOriginalFailure()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Offer");
        var vm = new PlayerVm();
        page.SetViewModel("Offer", vm);
        var picker = new Picker<string>();
        var failure = new IOException("Rejected edit");
        page.BindTwoWay<int, string>("Offer.Gold", value => picker.Display(value.ToString()), picker.Connect, text =>
        {
            vm.Gold = int.Parse(text);
            if (vm.Gold == 5) throw failure;
        });
        Assert.Same(failure, Assert.Throws<IOException>(() => picker.Edit("5")));
        Assert.Contains(failure, host.Errors);
        Assert.Equal(5, vm.Gold);
        Assert.Equal(new[] { "0" }, picker.Displayed);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { "0", "5" }, picker.Displayed);
        picker.Edit("6");
        vm.Gold = 7;
        ui.GetMvvm().FlushBindings();
        Assert.Equal("7", picker.Displayed.Last());
    }

    [Fact]
    public async Task CommitErrorKeepsItsIdentityWhenDisplayingTheRemainingStateAlsoFails()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Shop");
        var cart = new PlayerVm();
        page.SetViewModel("Cart", cart);
        var picker = new Picker<int>();
        var saveError = new IOException("Save failed");
        var displayError = new IOException("Display failed");
        page.BindTwoWay<int, int>("Cart.Gold", value =>
        {
            if (value == 5) throw displayError;
            picker.Display(value);
        }, picker.Connect, value =>
        {
            cart.Gold = value;
            if (value == 5) throw saveError;
        }, timing: BindingUpdateTiming.Immediate);
        Assert.Same(saveError, Assert.Throws<IOException>(() => picker.Edit(5)));
        Assert.Contains(displayError, UIFailure.GetSecondaryExceptions(saveError));
        picker.Edit(6);
        Assert.Equal(new[] { 0, 6 }, picker.Displayed);
    }

    [Fact]
    public async Task ThreeInputsResolveCurrentNestedOwnersAndDisposeWhileCommitting()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Shop");
        var old = new PlayerVm { Gold = 10 };
        var vm = new PlayerVm { Selected = old, Amount = 2 };
        page.SetViewModel("Shop", vm);
        var picker = new Picker<int>();
        IDisposable? binding = null;
        binding = page.BindTwoWay<int, int, int, int>("Shop.Selected.Gold", "Shop.Amount", "Shop.Gold",
            (price, count, discount) => picker.Display(price * count - discount), picker.Connect, edit =>
            {
                vm.Selected!.Gold = edit;
                binding!.Dispose();
            });
        vm.Selected = new PlayerVm { Gold = 3 };
        old.Gold = 100;
        ui.GetMvvm().FlushBindings();
        Assert.Equal(new[] { 20, 6 }, picker.Displayed);
        picker.Edit(9);
        Assert.Equal(9, vm.Selected.Gold);
        Assert.Equal(new[] { 20, 6 }, picker.Displayed);
        Assert.Equal(1, picker.Disconnections);
    }

    [Fact]
    public async Task DirectBindingRejectsReadOnlyPropertyBeforeCallingTheControl()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Offer");
        page.SetViewModel("Player", new PlayerVm());
        var picker = new Picker<bool>();
        var error = Assert.Throws<InvalidOperationException>(() =>
            page.BindTwoWay<bool>("Player.CanBuy", picker.Display, picker.Connect));
        Assert.Contains("Player.CanBuy", error.Message);
        Assert.Empty(picker.Displayed);
        Assert.Equal(0, picker.Connections);
    }

    [Fact]
    public async Task FailedInputSubscriptionReleasesSourceObserver()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Shop");
        var vm = new PlayerVm();
        page.SetViewModel("Cart", vm);
        var failure = new IOException("Picker connection");
        var renders = 0;
        var error = Assert.Throws<IOException>(() =>
            page.BindTwoWay<int>("Cart.Amount", _ => renders++, _ => throw failure));
        Assert.Same(failure, error);
        var countAfterFailure = renders;
        vm.Amount = 11;
        Assert.Equal(countAfterFailure, renders);
        Assert.Throws<KeyNotFoundException>(() => page.PauseBinding("Cart.Amount"));
    }

    [Fact]
    public async Task ClosingDuringSubscriptionDisconnectsTheJustAttachedInput()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Shop");
        var vm = new PlayerVm { Gold = 12 };
        page.SetViewModel("Cart", vm);
        var picker = new Picker<int>();
        ValueTask closing = default;
        using var binding = page.BindTwoWay<int>("Cart.Gold", picker.Display, edited =>
        {
            var disconnect = picker.Connect(edited);
            closing = page.DisposeAsync();
            return disconnect;
        });
        await closing;
        picker.LatestListener!(42);
        Assert.Equal(12, vm.Gold);
        Assert.Equal(1, picker.Disconnections);
    }

    [Fact]
    public async Task DirectEditAfterNestedReplacementWritesOnlyTheCurrentOwner()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var page = new UINode();
        await page.AttachAsync(ui.Root, "Shop");
        var old = new PlayerVm { Gold = 1 };
        var next = new PlayerVm { Gold = 9 };
        var cart = new PlayerVm { Selected = old };
        page.SetViewModel("Cart", cart);
        var picker = new Picker<int>();
        page.BindTwoWay<int>("Cart.Selected.Gold", picker.Display, picker.Connect);
        page.PauseBinding("Cart.Selected.Gold");
        cart.Selected = next;
        picker.Edit(8);
        page.ResumeBinding("Cart.Selected.Gold");
        Assert.Equal(9, picker.Value);
        picker.Edit(10);
        Assert.Equal(10, next.Gold);
        Assert.Equal(1, old.Gold);
    }
}
