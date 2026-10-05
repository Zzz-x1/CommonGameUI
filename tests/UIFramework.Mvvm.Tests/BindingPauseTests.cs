using System.Collections.ObjectModel;
using UIFramework.Mvvm;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed class BindingPauseTests
{
    private sealed class Host : IUIHost, IUILog
    {
        public readonly List<Exception> Errors = new();
        public void VerifyAccess() { }
        public object? CaptureFocus() => null;
        public void Focus(IUIElement? element) { }
        public void RestoreFocus(object? value) { }
        public void Error(Exception error, string context) => Errors.Add(error);
    }
    private sealed class Loader : IUIResourceLoader
    { public Task<IUIResource> LoadAsync(string path, CancellationToken token) => throw new NotSupportedException(); }
    private sealed class Target : IBindingTarget
    {
        public int Value; public bool Disposed; public Type ValueType => typeof(int);
        public object Read() => Value;
        public void Write(object? value) { Value = (int)value!; Changed?.Invoke(); }
        public event Action? Changed;
        public void Edit(int value) { Value = value; Changed?.Invoke(); }
        public void Dispose() => Disposed = true;
    }
    private sealed class Button : ICommandTarget
    {
        public event Action? Invoked;
        public void SetEnabled(bool enabled) { }
        public void Click() => Invoked?.Invoke();
        public void Dispose() { }
    }
    private sealed class Items : ICollectionTarget
    {
        public readonly List<object> Values = new();
        public void Insert(int index, object item) => Values.Insert(index == int.MaxValue ? Values.Count : index, item);
        public void Remove(int index) => Values.RemoveAt(index);
        public void Move(int oldIndex, int newIndex) { var item = Values[oldIndex]; Values.RemoveAt(oldIndex); Values.Insert(newIndex, item); }
        public void Clear() => Values.Clear();
        public void Dispose() => Clear();
    }
    private static UIContext Context(Host? host = null)
    { host ??= new Host(); return new UIContext(host, new Loader(), host); }
    private static async Task<UINode> Node(UIContext ui, string name = "View")
    { var node = new UINode(); await node.AttachAsync(ui.Root, name); return node; }

    [Theory]
    [InlineData(BindingMode.OneTime)]
    [InlineData(BindingMode.OneWay)]
    [InlineData(BindingMode.OneWayToViewModel)]
    [InlineData(BindingMode.TwoWay)]
    public async Task PauseBlocksBothDirectionsAndResumeReadsLatestForEveryMode(BindingMode mode)
    {
        await using var ui = Context(); var node = await Node(ui);
        var vm = new PlayerVm { Gold = 60 }; node.SetViewModel("Player", vm);
        var target = new Target { Value = 12 }; node.Bind("Player.Gold", target, mode);
        var before = vm.Gold;
        node.PauseBinding(nameof(PlayerVm.Gold)); target.Edit(1); Assert.Equal(before, vm.Gold);
        vm.Gold = 80; Assert.Equal(1, target.Value);
        node.ResumeBinding("Player.Gold"); Assert.Equal(80, target.Value); Assert.Equal(80, vm.Gold);
        target.Edit(2); Assert.Equal(mode is BindingMode.TwoWay or BindingMode.OneWayToViewModel ? 2 : 80, vm.Gold);
        vm.Gold = 90; Assert.Equal(mode is BindingMode.OneWay or BindingMode.TwoWay ? 90 : 2, target.Value);
    }
    [Fact] public async Task MatchingControlsPauseTogetherButOtherPropertiesAndChildNodesContinue()
    {
        await using var ui = Context(); var node = await Node(ui); var child = new UINode(); await child.AttachAsync(node, "Child");
        var vm = new PlayerVm { Gold = 20, Amount = 10 }; node.SetViewModel("Player", vm); child.SetViewModel("Player", vm);
        var first = new Target(); var second = new Target(); var unrelated = new Target(); var childTarget = new Target();
        node.Bind("Player.Gold", first); node.Bind("Player.Gold", second); node.Bind("Player.Amount", unrelated); child.Bind("Player.Gold", childTarget);
        node.PauseBinding("Gold"); vm.Gold = 40; vm.Amount = 30;
        Assert.Equal(20, first.Value); Assert.Equal(20, second.Value); Assert.Equal(30, unrelated.Value); Assert.Equal(40, childTarget.Value);
        node.ResumeBinding("Gold"); Assert.Equal(40, first.Value); Assert.Equal(40, second.Value);
    }
    [Fact] public async Task ShortNamesRequireOneSourcePathAndErrorsDoNotChangeBindingState()
    {
        var host = new Host(); await using var ui = Context(host); var node = await Node(ui);
        var player = new PlayerVm { Gold = 10 }; var shop = new PlayerVm { Gold = 20 }; node.SetViewModel("Player", player); node.SetViewModel("Shop", shop);
        var left = new Target(); var right = new Target(); node.Bind("Player.Gold", left); node.Bind("Shop.Gold", right);
        var ambiguous = Assert.Throws<InvalidOperationException>(() => node.PauseBinding("Gold")); Assert.Contains(ambiguous, host.Errors);
        player.Gold = 30; shop.Gold = 40; Assert.Equal(30, left.Value); Assert.Equal(40, right.Value);
        node.PauseBinding("Shop.Gold"); shop.Gold = 50; Assert.Equal(40, right.Value); node.ResumeBinding("Shop.Gold"); Assert.Equal(50, right.Value);
        Assert.Throws<KeyNotFoundException>(() => node.PauseBinding("Missing")); Assert.Throws<ArgumentException>(() => node.PauseBinding(""));
    }
    [Fact] public async Task PausedNestedPathTracksReplacementsAndNullPresentation()
    {
        await using var ui = Context(); var node = await Node(ui);
        var old = new PlayerVm { Gold = 10 }; var vm = new PlayerVm { Selected = old }; node.SetViewModel("Player", vm);
        var target = new Target(); node.Bind("Player.Selected.Gold", target, new BindingOptions { HasNullValue = true, NullValue = -1 });
        node.PauseBinding("Gold"); vm.Selected = new PlayerVm { Gold = 30 }; old.Gold = 99; Assert.Equal(10, target.Value);
        node.ResumeBinding("Gold"); Assert.Equal(30, target.Value);
        node.PauseBinding("Gold"); vm.Selected = null; node.ResumeBinding("Gold"); Assert.Equal(-1, target.Value);
    }
    [Fact] public async Task PairingAndWholeGroupValidationAreStrict()
    {
        await using var ui = Context(); var node = await Node(ui); var vm = new PlayerVm { Gold = 10 }; node.SetViewModel("Player", vm);
        var first = new Target(); node.Bind("Player.Gold", first);
        Assert.Throws<InvalidOperationException>(() => node.ResumeBinding("Gold")); node.PauseBinding("Gold");
        Assert.Throws<InvalidOperationException>(() => node.PauseBinding("Gold"));
        var second = new Target(); node.Bind("Player.Gold", second);
        Assert.Throws<InvalidOperationException>(() => node.ResumeBinding("Gold"));
        vm.Gold = 20; Assert.Equal(10, first.Value); Assert.Equal(20, second.Value);
    }
    [Fact] public async Task ResumeReportsOriginalErrorAndStillRefreshesOtherTargets()
    {
        var host = new Host(); await using var ui = Context(host); var node = await Node(ui);
        var vm = new PlayerVm { Gold = 10 }; node.SetViewModel("Player", vm); var first = new Target(); var second = new Target(); var last = new Target();
        var failure = new IOException("original conversion"); var cleanupFailure = new FormatException("second conversion");
        node.Bind("Player.Gold", first, new BindingOptions { Convert = value => (int)value! == 10 ? value : throw failure });
        node.Bind("Player.Gold", second, new BindingOptions { Convert = value => (int)value! == 10 ? value : throw cleanupFailure });
        node.Bind("Player.Gold", last); node.PauseBinding("Gold"); vm.Gold = 25;
        Assert.Same(failure, Assert.Throws<IOException>(() => node.ResumeBinding("Gold")));
        Assert.Contains(failure, host.Errors); Assert.Contains(cleanupFailure, host.Errors); Assert.Equal(25, last.Value);
        Assert.Contains(cleanupFailure, (List<Exception>)failure.Data[UIFailure.SecondaryExceptionsKey]!);
        Assert.Throws<InvalidOperationException>(() => node.ResumeBinding("Gold"));
    }
    [Fact] public async Task DisposedBindingsAreExcludedAndPausedNodeCleansUpNormally()
    {
        await using var ui = Context(); var node = await Node(ui); var vm = new PlayerVm { Gold = 10 }; node.SetViewModel("Player", vm);
        var removed = new Target(); var handle = node.Bind("Player.Gold", removed); handle.Dispose();
        Assert.Throws<KeyNotFoundException>(() => node.PauseBinding("Gold"));
        var target = new Target(); node.Bind("Player.Gold", target); node.PauseBinding("Gold"); await node.DisposeAsync();
        vm.Gold = 90; Assert.Equal(10, target.Value); Assert.True(target.Disposed);
        Assert.Throws<ObjectDisposedException>(() => node.ResumeBinding("Gold")); Assert.Throws<ObjectDisposedException>(() => node.PauseBinding("Gold"));
    }
    [Fact] public async Task CommandsCollectionsAndConstantBindingsAreNotPauseTargets()
    {
        await using var ui = Context(); var node = await Node(ui);
        var vm = new PlayerVm { Gold = 200 }; node.SetViewModel("Player", vm);
        var target = new Target(); node.Bind("Player.Gold", target); var constant = new Target(); node.Bind("Player.Amount", constant, new BindingOptions { HasConstantValue = true, ConstantValue = 7 });
        var button = new Button(); node.BindCommand("Player.Buy", button); var items = new Items(); node.BindCollection("Player.Items", items);
        Assert.Throws<KeyNotFoundException>(() => node.PauseBinding("Buy")); Assert.Throws<KeyNotFoundException>(() => node.PauseBinding("Items")); Assert.Throws<KeyNotFoundException>(() => node.PauseBinding("Amount"));
        node.PauseBinding("Gold"); button.Click(); await ui.WhenMvvmIdleAsync(); vm.Items.Add(new PlayerVm());
        Assert.Equal(100, vm.Gold); Assert.Equal(200, target.Value); Assert.Single(items.Values);
        node.ResumeBinding("Gold"); Assert.Equal(100, target.Value);
    }
}
