using UIFramework.Mvvm;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed partial class CollectionItemTests
{
    [Fact]
    public async Task ReplacingSelectionPreservesInventoryRowsAndPendingCreation()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var owner = new UINode();
        await owner.AttachAsync(ui.Root, "Inventory");
        var list = new CollectionVm();
        var pending = new ParameterVm();
        list.Items.Add(new ParameterVm());
        list.Items.Add(pending);
        owner.SetViewModel("Player", list);
        var target = new Target(owner);
        var gate = target.Gates[pending] = new();
        try
        {
            owner.BindCollection("Player.Items", target);
            var first = Assert.Single(target.Ordered);
            owner.SetViewModel("Selection", new ParameterVm());
            owner.CreateViewModel<ParameterVm>("Compare");
            Assert.Same(first, Assert.Single(target.Ordered));
            Assert.Equal(2, target.Contexts.Count);
            gate.SetResult(true);
            await ui.WhenMvvmIdleAsync();
            Assert.Same(first, target.Ordered[0]);
            Assert.Same(pending, target.Ordered[1].GetViewModel<ParameterVm>("Item"));
            Assert.All(target.Contexts, row => Assert.Equal(0, row.Display.Releases));
        }
        finally { gate.TrySetResult(true); }
    }

    [Fact]
    public async Task NestedOwnerReplacementReusesSharedCollectionUntilInstanceChangesOrResets()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var owner = new UINode();
        await owner.AttachAsync(ui.Root, "Inventory");
        var old = new OwnedVm();
        old.Items.Add(new OwnedVm());
        var player = new OwnedVm { Other = old };
        owner.SetViewModel("Player", player);
        var target = new Target(owner);
        owner.BindCollection("Player.Other.Items", target);
        var first = Assert.Single(target.Ordered);
        var next = new OwnedVm { Items = old.Items };
        player.Other = next;
        old.Items = new();
        Assert.Same(first, Assert.Single(target.Ordered));
        Assert.Single(target.Contexts);

        var previousItems = next.Items;
        next.Items = new() { new OwnedVm() };
        previousItems.Add(new OwnedVm());
        Assert.NotSame(first, Assert.Single(target.Ordered));
        Assert.Equal(2, target.Contexts.Count);
        next.Items.Clear();
        await ui.WhenMvvmIdleAsync();
        Assert.Empty(target.Ordered);
        Assert.All(target.Contexts, row => Assert.Equal(1, row.Display.Releases));
    }
}
