using System.Collections.ObjectModel;
using System.ComponentModel;
using UIFramework.Mvvm;
using Xunit;

namespace UIFramework.Mvvm.Tests;
[ViewModel]
public class PlayerVm
{
    [FieldNotify] public int Gold { get; set; }
    [FieldNotify(nameof(Gold))] public bool CanBuy => Gold >= 100;
    [FieldNotify] public PlayerVm? Selected { get; set; }
    [FieldNotify] public ObservableCollection<PlayerVm> Items { get; set; } = new();
    public int SetterCalls { get; private set; }
    private int _amount;
    [FieldNotify] public int Amount { get => _amount; set { SetterCalls++; _amount = Math.Clamp(value, 0, 100); } }
    [Command(CanExecute = nameof(CanBuy))] public void Buy() => Gold -= 100;
    [Command] public Task Fail() => Task.FromException(Failure);
    public Exception Failure { get; } = new IOException("original command error");
}
[ViewModel] public class ExtendedVm : PlayerVm { [FieldNotify] public string Name { get; set; } = ""; [FieldNotify(nameof(Gold))] public bool Rich => Gold >= 1000; }
public static class TestConverters { [Converter] public static string Gold(int value) => "G" + value; }
public sealed class MvvmTests
{
    private sealed class Host : IUIHost
    { public void VerifyAccess() { } public object? CaptureFocus() => null; public void Focus(IUIElement? e) { } public void RestoreFocus(object? e) { } }
    private sealed class Loader : IUIResourceLoader { public Task<IUIResource> LoadAsync(string path, CancellationToken token) => throw new NotSupportedException(); }
    private sealed class Log : IUILog { public readonly List<Exception> Errors = new(); public void Error(Exception error, string context) => Errors.Add(error); }
    private sealed class Target : IBindingTarget
    {
        public Type ValueType => typeof(int); public int Value; public event Action? Changed;
        public object Read() => Value; public void Write(object? value) { Value = (int)value!; Changed?.Invoke(); }
        public void Edit(int value) { Value = value; Changed?.Invoke(); } public void Dispose() { }
    }
    private static IMvvmObject Object(object vm) => (IMvvmObject)vm;
    [Fact] public void StaticConvertersUseDirectWovenCalls() => Assert.Equal("G10", Converters.Find("UIFramework.Mvvm.Tests.TestConverters.Gold")(10));
    [Fact] public void SetterPreservesLogicAndComputedDependencies()
    {
        var vm = new PlayerVm(); var changes = new List<string>(); ((INotifyPropertyChanged)(object)vm).PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        vm.Gold = 100; vm.Gold = 100; Assert.Equal(new[] { "Gold", "CanBuy" }, changes);
        vm.Amount = 200; vm.Amount = 500; Assert.Equal(2, vm.SetterCalls); Assert.Equal(100, vm.Amount);
        Assert.Equal(1, changes.Count(p => p == "Amount"));
    }
    [Fact] public async Task DirectAccessAndCommands()
    {
        var vm = new PlayerVm(); var bound = Object(vm); bound.Write("Gold", 200); Assert.Equal(200, bound.Read("Gold"));
        Assert.True(bound.CanExecute("Buy")); await bound.Execute("Buy", null, default); Assert.Equal(100, vm.Gold);
        Assert.Same(vm.Failure, await Assert.ThrowsAsync<IOException>(() => bound.Execute("Fail", null, default)));
    }
    [Fact] public void DerivedVmObservesBaseProperties()
    { var vm = new ExtendedVm(); var names = new List<string>(); ((INotifyPropertyChanged)(object)vm).PropertyChanged += (_, e) => names.Add(e.PropertyName!); vm.Gold = 20; vm.Name = "A"; Assert.Equal(new[] { "Gold", "Rich", "CanBuy", "Name" }, names); Assert.Equal(20, Object(vm).Read("Gold")); }
    [Fact] public async Task TwoWayNormalizesAndUnsubscribes()
    {
        await using var runtime = new MvvmRuntime(); var ui = new UIContext(new Host(), new Loader()); ui.ConfigureMvvm(runtime);
        var node = new UINode(); await node.AttachAsync(ui.Root, "View"); var vm = new PlayerVm { Amount = 25 }; node.SetViewModel("Player", vm);
        var target = new Target(); node.Bind("Player.Amount", target, BindingMode.TwoWay); Assert.Equal(25, target.Value);
        target.Edit(150); Assert.Equal(100, target.Value); Assert.Equal(100, vm.Amount);
        await node.DisposeAsync(); vm.Amount = 50; Assert.Equal(100, target.Value); await ui.DisposeAsync();
    }
    [Fact] public async Task NestedReplacementAndNullPresentation()
    {
        await using var runtime = new MvvmRuntime(); var ui = new UIContext(new Host(), new Loader()); ui.ConfigureMvvm(runtime);
        var node = new UINode(); await node.AttachAsync(ui.Root, "View"); var old = new PlayerVm { Gold = 10 }; var vm = new PlayerVm { Selected = old }; node.SetViewModel("Player", vm);
        var target = new Target(); node.Bind("Player.Selected.Gold", target, new BindingOptions { HasNullValue = true, NullValue = -1 });
        vm.Selected = new PlayerVm { Gold = 30 }; old.Gold = 99; Assert.Equal(30, target.Value); vm.Selected = null; Assert.Equal(-1, target.Value); await ui.DisposeAsync();
    }
}
