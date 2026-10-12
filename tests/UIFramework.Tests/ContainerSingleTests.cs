using UIFramework;
using Xunit;

namespace UIFramework.Tests;

public class ContainerSingleTests
{
    private sealed class DerivedCounter : CounterView { }
    private static UIContext Create()
    {
        var host = new TestHost();
        var ui = new UIContext(host, host, host);
        ui.Register<CounterView>(() => new DerivedCounter());
        ui.Register<ArgumentView>(() => new ArgumentView());
        return ui;
    }
    private static async Task<ContainerNode> Attach(UIContext ui, bool modal)
    {
        ContainerNode container = modal ? new ModalContainerNode() : new StackContainerNode();
        await container.AttachAsync(ui.Root, "Windows", new MemoryElement());
        return container;
    }
    private static Task<T> Open<T>(ContainerNode container, object? args = null) where T : class, IUINode =>
        container is ModalContainerNode modals ? modals.OpenAsync<T>(args, playAnimation: false) :
        ((StackContainerNode)container).PushAsync<T>(args, playAnimation: false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedSingleOpenWaitsForFirstCreationAndReusesIt(bool modal)
    {
        var host = new TestHost(); await using var ui = new UIContext(host, host, host);
        ui.Register<CounterView>(() => new CounterView(), "window");
        var container = await Attach(ui, modal);
        host.LoadGate = new TaskCompletionSource<bool>();
        var first = Open<CounterView>(container);
        var second = Open<CounterView>(container);
        Assert.False(second.IsCompleted);
        host.LoadGate.SetResult(true);
        Assert.Same(await first, await second);
        Assert.Single(host.Loaded);
        Assert.Single(container.Children);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleReusesRegisteredIdentityAndReturnsFromLowerHistory(bool modal)
    {
        await using var ui = Create();
        var container = await Attach(ui, modal);
        var first = await Open<CounterView>(container);
        var calls = await Task.WhenAll(Open<CounterView>(container), Open<CounterView>(container));
        Assert.All(calls, item => Assert.Same(first, item));
        var top = await Open<ArgumentView>(container, "item");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Open<CounterView>(container, "new"));
        Assert.Equal(NodeState.Ready, top.State);
        Assert.Equal(2, container.Children.Count);
        Assert.Same(first, await Open<CounterView>(container));
        Assert.Equal(NodeState.Disposed, top.State);
        Assert.True(first.IsInteractive);
        Assert.Single(container.Children);
        Assert.Equal(1, first.Initialized);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Open<CounterView>(container, "new"));
        Assert.Same(first, await Open<CounterView>(container));
    }

    [Fact]
    public async Task StackSingleIdentityIsLocalToEachContainer()
    {
        await using var ui = Create();
        var first = await Attach(ui, false);
        var second = await Attach(ui, false);
        var a = await Open<CounterView>(first);
        var b = await Open<CounterView>(second);
        Assert.NotSame(a, b);
        Assert.Same(a, await Open<CounterView>(first));
        Assert.Same(b, await Open<CounterView>(second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipleStillCreatesIndependentHistoryItems(bool modal)
    {
        var host = new TestHost(); await using var ui = new UIContext(host, host, host);
        ui.Register<CounterView>(() => new CounterView(), windowMode: WindowInstanceMode.Multiple);
        var container = await Attach(ui, modal);
        Assert.NotSame(await Open<CounterView>(container), await Open<CounterView>(container));
        Assert.Equal(2, container.Children.Count);
    }

    [Fact]
    public async Task ConcurrentSingleDialogsShareTheExistingResultSession()
    {
        await using var ui = Create(); ui.Register<BoolDialog>(() => new BoolDialog());
        var modals = (ModalContainerNode)await Attach(ui, true);
        var first = modals.ShowDialogAsync<BoolDialog, bool>(playAnimation: false);
        var second = modals.ShowDialogAsync<BoolDialog, bool>(playAnimation: false);
        var dialog = Assert.IsType<BoolDialog>(modals.Current);
        Assert.Single(modals.Children);
        dialog.Complete(true);
        Assert.True(await first); Assert.True(await second);
        Assert.Null(modals.Current);
    }
}
