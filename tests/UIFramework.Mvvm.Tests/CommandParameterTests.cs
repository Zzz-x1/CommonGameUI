using UIFramework.Mvvm;
using Xunit;

namespace UIFramework.Mvvm.Tests;
[ViewModel] public class ParameterVm
{
    [FieldNotify] public int Id { get; set; }
    [FieldNotify] public ParameterVm? Selection { get; set; }
    public int Received;
    [Command] public void Buy(int id) => Received = id;
}
public sealed class CommandParameterTests
{
    private sealed class Host : IUIHost
    { public void VerifyAccess() { } public object? CaptureFocus() => null; public void Focus(IUIElement? element) { } public void RestoreFocus(object? value) { } }
    private sealed class Loader : IUIResourceLoader
    { public Task<IUIResource> LoadAsync(string path, CancellationToken token) => throw new NotSupportedException(); }
    private sealed class Button : ICommandTarget
    { public event Action? Invoked; public void Press() => Invoked?.Invoke(); public void SetEnabled(bool value) { } public void Dispose() { } }
    [Fact] public async Task ParameterPathReadsLatestNestedValue()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var node = new UINode(); await node.AttachAsync(ui.Root, "Test");
        var vm = new ParameterVm { Selection = new ParameterVm { Id = 1 } }; node.SetViewModel("Shop", vm);
        var button = new Button(); node.BindCommand("Shop.Buy", button, new CommandBindingOptions { ParameterPath = "Shop.Selection.Id" });
        vm.Selection = new ParameterVm { Id = 9 }; button.Press(); await ui.WhenMvvmIdleAsync(); Assert.Equal(9, vm.Received);
        vm.Selection = null; Assert.Throws<ArgumentException>(() => button.Press());
    }
    [Fact] public async Task ConstantsAreParsedOnceAndTypesAreStrict()
    {
        await using var ui = new UIContext(new Host(), new Loader()); var node = new UINode(); await node.AttachAsync(ui.Root, "Test");
        var vm = new ParameterVm(); node.SetViewModel("Shop", vm); var button = new Button();
        node.BindCommand("Shop.Buy", button, new CommandBindingOptions { Mode = CommandParameterMode.Constant, ConstantText = "42" }); button.Press(); await ui.WhenMvvmIdleAsync(); Assert.Equal(42, vm.Received);
        Assert.Equal(typeof(int), ((IMvvmObject)vm).Describe().Single(m => m.Name == "Buy").ParameterType);
        Assert.Throws<InvalidCastException>(() => node.BindCommand("Shop.Buy", new Button(), "42"));
        Assert.Throws<ArgumentException>(() => node.BindCommand("Shop.Buy", new Button()));
        Assert.Throws<FormatException>(() => node.BindCommand("Shop.Buy", new Button(), new CommandBindingOptions { Mode = CommandParameterMode.Constant, ConstantText = "bad" }));
    }
}
