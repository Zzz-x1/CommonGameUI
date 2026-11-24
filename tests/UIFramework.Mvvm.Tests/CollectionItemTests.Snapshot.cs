using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed partial class CollectionItemTests
{
    [Fact]
    public async Task SnapshotReversesOneHundredRowsWithoutRebuildingDisplays()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var owner = new UINode();
        await owner.AttachAsync(ui.Root, "Inventory");
        var vm = new CollectionVm();
        owner.SetViewModel("Player", vm);
        var target = new Target(owner);
        owner.BindCollection("Player.Items", target);
        var ids = Enumerable.Range(1, 100).ToArray();
        void Apply(int[] ordered) => vm.Items.ApplySnapshot(ordered, row => row.Id, id => id,
            id => new ParameterVm { Id = id }, (row, id) => row.Received = id * 2);

        Apply(ids);
        await ui.WhenMvvmIdleAsync();
        var original = target.Ordered.ToArray();
        Apply(ids.Reverse().ToArray());
        await ui.WhenMvvmIdleAsync();
        Assert.Equal(original.Reverse(), target.Ordered);
        Assert.Equal(100, target.Contexts.Count);
        Assert.All(target.Contexts, item => Assert.Equal(0, item.Display.Releases));
        Assert.All(vm.Items, row => Assert.Equal(row.Id * 2, row.Received));
    }

    [Fact]
    public async Task SnapshotMovesPendingItemAndDisposesOnlyTheRemovedLateItem()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var owner = new UINode();
        await owner.AttachAsync(ui.Root, "Shop");
        var vm = new CollectionVm();
        owner.SetViewModel("Player", vm);
        var target = new Target(owner);
        owner.BindCollection("Player.Items", target);
        var moved = new ParameterVm { Id = 1 };
        var removed = new ParameterVm { Id = 2 };
        var ready = new ParameterVm { Id = 3 };
        var moveGate = target.Gates[moved] = new();
        var removeGate = target.Gates[removed] = new();
        try
        {
            vm.Items.Add(moved);
            vm.Items.Add(removed);
            vm.Items.Add(ready);
            var originalDisplay = Assert.Single(target.Ordered);
            vm.Items.ApplySnapshot(new[] { 3, 1 }, row => row.Id, id => id,
                _ => throw new InvalidOperationException("Unexpected creation"), (row, id) => row.Received = id);
            removeGate.SetResult(true);
            moveGate.SetResult(true);
            await ui.WhenMvvmIdleAsync();

            Assert.Equal(3, target.Contexts.Count);
            Assert.Equal(1, target.Contexts[1].Display.Releases);
            Assert.Same(originalDisplay, target.Ordered[0]);
            Assert.Equal(new[] { ready, moved }, target.Ordered.Select(node => node.GetViewModel<ParameterVm>("Item")));
        }
        finally { moveGate.TrySetResult(true); removeGate.TrySetResult(true); }
    }
}
