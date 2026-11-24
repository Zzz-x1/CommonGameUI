using System.ComponentModel;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed class ManualNotifyTests
{
    [ViewModel]
    private class CombatStats
    {
        internal int ServiceHealth = 10;
        [FieldNotify] public int Health => ServiceHealth;
        [FieldNotify(nameof(Health))] public bool IsDefeated => Health <= 0;
        [FieldNotify(nameof(Health))] public int MissingHealth => 100 - Health;
        public string Status => IsDefeated ? "defeated" : "alive";
        internal void OnServiceChanged() => this.NotifyChanged(nameof(Health));
    }

    [ViewModel]
    private sealed class PlayerStats : CombatStats
    {
        [FieldNotify(nameof(IsDefeated), nameof(MissingHealth))] public bool ShowWarning => MissingHealth > 80 || IsDefeated;
    }

    [Fact]
    public void ReadOnlyServicePropertyNotifiesInheritedAndTransitiveDependantsOnce()
    {
        var vm = new PlayerStats();
        var changes = new List<string>();
        ((INotifyPropertyChanged)(object)vm).PropertyChanged += (_, args) => changes.Add(args.PropertyName!);
        vm.ServiceHealth = 0;
        vm.OnServiceChanged();
        Assert.Equal(new[] { "Health", "IsDefeated", "MissingHealth", "ShowWarning" }, changes);
        changes.Clear();
        vm.NotifyChanged(nameof(PlayerStats.ShowWarning));
        Assert.Equal(new[] { "ShowWarning" }, changes);
    }

    [Fact]
    public void ManualNotificationIsUnconditionalAndDoesNotInvokeSetter()
    {
        var vm = new PlayerVm();
        vm.NotifyChanged(nameof(PlayerVm.Amount)); // Valid even without subscribers.
        var notifications = new List<string>();
        ((INotifyPropertyChanged)(object)vm).PropertyChanged += (_, args) => notifications.Add(args.PropertyName!);
        vm.Amount = 0;
        Assert.Empty(notifications);
        vm.NotifyChanged(nameof(PlayerVm.Amount));
        vm.NotifyChanged(nameof(PlayerVm.Amount));
        Assert.Equal(new[] { "Amount", "Amount" }, notifications);
        Assert.Equal(1, vm.SetterCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData(nameof(CombatStats.Status))]
    public void InvalidPropertyNamesIdentifyTheViewModelAndMember(string name)
    {
        var vm = new PlayerStats();
        var error = Assert.Throws<MissingMemberException>(() => vm.NotifyChanged(name));
        Assert.Contains(typeof(PlayerStats).FullName!, error.Message);
        Assert.Contains($"'{name}'", error.Message);
    }

    [Fact]
    public void RejectsAnUnwovenReceiverAndPropagatesTheOriginalObserverError()
    {
        var notWoven = new object();
        var invalid = Assert.Throws<InvalidOperationException>(() => notWoven.NotifyChanged("Health"));
        Assert.Contains("System.Object", invalid.Message);
        Assert.Contains("Health", invalid.Message);

        var vm = new PlayerVm();
        var expected = new IOException("Display unavailable");
        ((INotifyPropertyChanged)(object)vm).PropertyChanged += (_, _) => throw expected;
        Assert.Same(expected, Assert.Throws<IOException>(() => vm.NotifyChanged(nameof(PlayerVm.Gold))));
    }
}
