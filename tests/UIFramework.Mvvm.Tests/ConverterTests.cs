using UIFramework.Mvvm;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public static class EditorConverters
{
    public static readonly Exception Failure = new InvalidOperationException("converter failed");
    [Converter] public static string Amount(int value) => "Gold " + value;
    [Converter] public static int Parse(string value) => int.Parse(value);
    [Converter] public static string Fail(int value) => throw Failure;
}
public sealed class ConverterTests
{
    private const string Prefix = "UIFramework.Mvvm.Tests.EditorConverters.";
    private sealed class Host : IUIHost
    { public void VerifyAccess() { } public object? CaptureFocus() => null; public void Focus(IUIElement? element) { } public void RestoreFocus(object? value) { } }
    private sealed class Loader : IUIResourceLoader
    { public Task<IUIResource> LoadAsync(string path, CancellationToken token) => throw new NotSupportedException(); }
    private sealed class Target : IBindingTarget
    {
        public Type ValueType => typeof(string);
        public string Value = "";
        public bool Disposed;
        public object Read() => Value;
        public void Write(object? value) { Value = (string)value!; Changed?.Invoke(); }
        public event Action? Changed;
        public void Edit(string value) { Value = value; Changed?.Invoke(); }
        public void Dispose() => Disposed = true;
    }
    private static BindingOptions Options(string name, ConverterInput input = ConverterInput.ViewModel, string literal = "", BindingMode mode = BindingMode.OneWay, string reverse = "") =>
        Converters.Configure(Prefix + name, reverse.Length == 0 ? "" : Prefix + reverse, mode, input, literal, false, null);

    [Fact] public async Task MemberInputObservesNestedReplacementAndReleasesSubscriptions()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var node = new UINode(); await node.AttachAsync(ui.Root, "View");
        var old = new PlayerVm { Gold = 10 }; var vm = new PlayerVm { Selected = old }; node.SetViewModel("Player", vm);
        var target = new Target(); node.Bind("Player.Selected.Gold", target, Options("Amount"));
        Assert.Equal("Gold 10", target.Value);
        vm.Selected = new PlayerVm { Gold = 25 }; old.Gold = 99;
        Assert.Equal("Gold 25", target.Value);
        vm.Selected.Gold = 40; Assert.Equal("Gold 40", target.Value);
        await node.DisposeAsync(); vm.Selected.Gold = 50;
        Assert.Equal("Gold 40", target.Value); Assert.True(target.Disposed);
    }
    [Fact] public async Task ConstantInputRequiresNoFakeViewModelAndIsAppliedOnce()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var node = new UINode(); await node.AttachAsync(ui.Root, "View");
        var target = new Target(); node.Bind("", target, Options("Amount", ConverterInput.Constant, "123"));
        Assert.Equal("Gold 123", target.Value);
        target.Edit("local"); Assert.Equal("local", target.Value);
    }
    [Fact] public async Task TwoWayUsesSeparatelyConfiguredReverseConverter()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var node = new UINode(); await node.AttachAsync(ui.Root, "View");
        var vm = new PlayerVm { Gold = 7 }; node.SetViewModel("Player", vm);
        var target = new Target();
        node.Bind("Player.Gold", target, Converters.Configure("FormatNumber", Prefix + "Parse", BindingMode.TwoWay, ConverterInput.ViewModel, "", false, null));
        Assert.Equal("7", target.Value); target.Edit("42"); Assert.Equal(42, vm.Gold); Assert.Equal("42", target.Value);
    }
    [Theory]
    [InlineData(BindingMode.TwoWay)]
    [InlineData(BindingMode.OneWayToViewModel)]
    public void ConstantsCannotPretendToBeWritableVmMembers(BindingMode mode) =>
        Assert.Throws<InvalidOperationException>(() => Options("Amount", ConverterInput.Constant, "10", mode));
    [Fact] public void InvalidLiteralsAndMissingConvertersFailDirectly()
    {
        Assert.Throws<FormatException>(() => Options("Amount", ConverterInput.Constant, "not a number"));
        Assert.Throws<InvalidOperationException>(() => Converters.Configure("Missing", "", BindingMode.OneWay, ConverterInput.ViewModel, "", false, null));
    }
    [Fact] public async Task ConverterExceptionRetainsIdentityAndReleasesTarget()
    {
        await using var ui = new UIContext(new Host(), new Loader());
        var node = new UINode(); await node.AttachAsync(ui.Root, "View");
        var target = new Target();
        Assert.Same(EditorConverters.Failure, Assert.Throws<InvalidOperationException>(() => node.Bind("", target, Options("Fail", ConverterInput.Constant, "10"))));
        Assert.True(target.Disposed);
    }
}
