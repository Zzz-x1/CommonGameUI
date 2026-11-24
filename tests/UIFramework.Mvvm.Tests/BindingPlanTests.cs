using System.ComponentModel;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public sealed class BindingPlanTests
{
    private sealed class Host : IUIHost, IUIResourceLoader, IUILog
    {
        public List<(Exception Error, string Context)> Errors { get; } = new();
        public void VerifyAccess() { }
        public object? CaptureFocus() => null;
        public bool ContainsFocus(IUIElement scope, object target) => false;
        public bool TryFocus(IUIElement? scope, object? target) => false;
        public void ClearFocus() { }
        public Task<IUIResource> LoadAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public void Exception(Exception error, string context) => Errors.Add((error, context));
        public void Error(string message) => throw new InvalidOperationException(message);
    }

    private class Product : IMvvmObject
    {
        internal int Descriptions, Price;
        public event PropertyChangedEventHandler? PropertyChanged;
        public object Read(string member) => member == "Price" ? Price : throw new MissingMemberException(member);
        public void Write(string member, object? value)
        {
            if (member != "Price") throw new MissingMemberException(member);
            Price = (int)value!;
            NotifyChanged(member);
        }
        public void NotifyChanged(string propertyName)
        {
            if (propertyName != "Price") throw new MissingMemberException(GetType().FullName, propertyName);
            MvvmNotifications.Raise(PropertyChanged, this, propertyName);
        }
        public bool CanExecute(string command) => throw new InvalidOperationException("Preview called CanExecute");
        public Task Execute(string command, object? parameter, CancellationToken token) => throw new InvalidOperationException("Preview ran a command");
        public virtual MvvmMember[] Describe()
        {
            Descriptions++;
            return new[] { new MvvmMember("Price", typeof(int), MvvmMemberKind.Property, true, true),
                new MvvmMember("Buy", typeof(int), MvvmMemberKind.Command, false, false) };
        }
    }
    private sealed class ReadOnlyProduct : Product
    {
        public override MvvmMember[] Describe() =>
            new[] { new MvvmMember("Price", typeof(int), MvvmMemberKind.Property, true, false) };
    }

    private class PriceLabel
    {
        internal int Value;
        internal Action? Changed;
        internal void Edit(int value) { Value = value; Changed?.Invoke(); }
    }
    private sealed class OverrideLabel : PriceLabel { }
    private sealed class Labels : IBindingTargetProvider
    {
        public IEnumerable<BindingTargetDefinition> GetTargets()
        {
            yield return BindingTargetDefinition.Create<PriceLabel, int>("Value", label => label.Value, (label, value) => label.Value = value,
                (label, changed) => { label.Changed += changed; return () => label.Changed -= changed; });
        }
    }
    private sealed class Overrides : IBindingTargetProvider
    {
        public IEnumerable<BindingTargetDefinition> GetTargets()
        {
            yield return BindingTargetDefinition.Create<OverrideLabel, int>("Value", label => label.Value, (label, value) => label.Value = value * 2);
        }
    }
    private sealed class CommandTarget : ICommandTarget
    {
        public event Action? Invoked;
        internal bool Connected => Invoked != null;
        internal bool Disposed;
        public void SetEnabled(bool value) { }
        public void Dispose() => Disposed = true;
    }
    private sealed class ListTarget : ICollectionTarget, IAsyncDisposable
    {
        internal int Inserts, Releases;
        internal readonly TaskCompletionSource Released = new();
        public void Insert(int index, object item) => Inserts++;
        public void Remove(int index) { }
        public void Move(int oldIndex, int newIndex) { }
        public void Clear() { }
        public void Dispose() => throw new InvalidOperationException("Use async release");
        public ValueTask DisposeAsync() { Releases++; return new ValueTask(Released.Task); }
    }
    private sealed class Slots(params (BindingRule Rule, object Target)[] rows) : IViewBindingSlots
    {
        public int Count => rows.Length;
        public bool IsEnabled(int slot) => true;
        public BindingRule GetRule(int slot) => rows[slot].Rule;
        public object GetTarget(int slot) => rows[slot].Target;
        public Type CommandTargetType => typeof(CommandTarget);
        public ICommandTarget CreateCommandTarget(object target) => (CommandTarget)target;
        public ICollectionTarget CreateCollectionTarget(IUINode owner, int slot, bool preview) => (ICollectionTarget)rows[slot].Target;
    }
    private static readonly BindingRule Price = new(ViewBindingKind.Property, "Item.Price", "Value", BindingMode.TwoWay);
    private static async Task<UINode> Row(UIContext ui, int index, Product model)
    {
        var row = new UINode();
        await row.AttachAsync(ui.Root, "Row" + index);
        row.SetViewModel("Item", model);
        return row;
    }

    [Theory]
    [InlineData(BindingMode.TwoWay)]
    [InlineData(BindingMode.OneWayToViewModel)]
    public async Task UnsupportedCodeBindingFailsBeforeEitherSideChanges(BindingMode mode)
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var model = new Product { Price = 21 };
        var node = await Row(ui, 1, model);
        var control = new TestControl { Value = 73 };
        var target = BindingTargetRegistry.Find(typeof(TestControl), "Display").CreateTarget(control);

        Assert.Throws<InvalidOperationException>(() => node.Bind("Item.Price", target, mode));
        Assert.Equal(21, model.Price);
        Assert.Equal(73, control.Value);
    }

    [Fact]
    public async Task ThirtyProductRowsShareDescriptionsAndReleaseTheirOwnConnections()
    {
        BindingTargetRegistry.Register(new Labels());
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var rows = new List<(UINode Node, Product Model, PriceLabel Target)>();
        for (int i = 0; i < 30; i++)
        {
            var model = new Product { Price = i };
            var row = await Row(ui, i, model);
            var label = new PriceLabel();
            ViewBindingPlans.Apply(row, new Slots((Price, label)));
            Assert.Equal(i, label.Value);
            rows.Add((row, model, label));
        }
        Assert.Equal(1, rows.Sum(row => row.Model.Descriptions));
        await rows[5].Node.DisposeAsync();
        rows[5].Model.Write("Price", 77);
        Assert.Equal(5, rows[5].Target.Value);
        Assert.Null(rows[5].Target.Changed);
        rows[6].Target.Edit(42);
        Assert.Equal(42, rows[6].Model.Price);
        rows[7].Model.Write("Price", 84);
        Assert.Equal(7, rows[7].Target.Value);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(84, rows[7].Target.Value);
        Assert.Equal(8, rows[8].Target.Value);
        rows[8].Node.SetViewModel("Item", new ReadOnlyProduct());
        Assert.Throws<InvalidOperationException>(ui.GetMvvm().FlushBindings);
    }

    [Fact]
    public async Task PreviewValidatesTheOriginalModeAndCommandParametersAndRollsBackConnectedRows()
    {
        BindingTargetRegistry.Register(new Labels());
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var model = new Product { Price = 42 };
        var row = await Row(ui, 1, model);
        var label = new PriceLabel();
        var command = new BindingRule(ViewBindingKind.Command, "Item.Buy", parameterMode: CommandParameterMode.SourcePath, parameterPath: "Item.Price");
        ViewBindingPlans.Apply(row, new Slots((Price, label), (command, new CommandTarget())), preview: true);
        Assert.Equal(42, label.Value);
        label.Edit(55);
        Assert.Equal(42, model.Price);
        var broken = new BindingRule(ViewBindingKind.Command, "Item.Buy", parameterMode: CommandParameterMode.Constant, parameterLiteral: "abc");
        var detached = new PriceLabel();
        var error = Assert.Throws<FormatException>(() => ViewBindingPlans.Apply(row, new Slots((Price, detached), (broken, new CommandTarget())), preview: true));
        Assert.Null(detached.Changed);
        model.Write("Price", 3);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(3, label.Value);
        Assert.Equal(42, detached.Value);
        Assert.Contains(host.Errors, entry => ReferenceEquals(entry.Error, error) && entry.Context.Contains("row 1") && entry.Context.Contains("Item.Buy"));
        var readOnly = await Row(ui, 2, new ReadOnlyProduct());
        Assert.Throws<InvalidOperationException>(() => ViewBindingPlans.Apply(readOnly, new Slots((Price, detached)), preview: true));
        Assert.Null(detached.Changed);
    }

    [ViewModel]
    public class NestedCommands
    {
        public OwnedVm Unobserved { get; } = new();
        [FieldNotify] public OwnedVm? Selected { get; set; }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreviewAndRuntimeValidateNestedCommandAndWriteOnlyPaths(bool preview)
    {
        BindingTargetRegistry.Register(new Labels());
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var node = await Row(ui, 1, new Product());
        node.SetViewModel("Shop", new NestedCommands());
        var command = new BindingRule(ViewBindingKind.Command, "Shop.Unobserved.Fail");
        var error = Assert.Throws<InvalidOperationException>(() =>
            ViewBindingPlans.Apply(node, new Slots((command, new CommandTarget())), preview));
        Assert.Contains("FieldNotify", error.Message);
        var writeOnly = new BindingRule(ViewBindingKind.Property, "Shop.Selected.Gold", "Value", BindingMode.OneWayToViewModel);
        var target = new PriceLabel();
        error = Assert.Throws<InvalidOperationException>(() => ViewBindingPlans.Apply(node, new Slots((writeOnly, target)), preview));
        Assert.Contains("null VM path", error.Message);
        Assert.Null(target.Changed);
    }

    [Fact]
    public async Task ConnectionFailureDisconnectsCommandsAndCollectionsAndAwaitsTheirRelease()
    {
        var host = new Host();
        await using var ui = new UIContext(host, host, host);
        var node = await Row(ui, 1, new Product());
        var model = new OwnedVm();
        model.Items.Add(new OwnedVm());
        node.SetViewModel("Shop", model);
        var list = new ListTarget();
        var button = new CommandTarget();
        var rows = new Slots((new BindingRule(ViewBindingKind.Collection, "Shop.Items"), list),
            (new BindingRule(ViewBindingKind.Command, "Shop.Fail"), button),
            (new BindingRule(ViewBindingKind.Property, "Shop.Gold", "Unknown"), new PriceLabel()));
        Assert.Throws<MissingMemberException>(() => ViewBindingPlans.Apply(node, rows));
        Assert.False(button.Connected);
        Assert.True(button.Disposed);
        Assert.Equal(1, list.Releases);
        model.Items.Add(new OwnedVm());
        Assert.Equal(1, list.Inserts);
        var close = node.DisposeAsync().AsTask();
        Assert.False(close.IsCompleted);
        list.Released.SetResult();
        await close;
        Assert.Equal(1, list.Releases);
    }

    [Fact]
    public async Task LateMoreSpecificRegistrationAffectsSubsequentConnections()
    {
        BindingTargetRegistry.Register(new Labels());
        var host = new Host();
        await using var ui = new UIContext(host, host);
        var model = new Product { Price = 6 };
        var first = new OverrideLabel();
        var second = new OverrideLabel();
        var rule = new BindingRule(ViewBindingKind.Property, "Item.Price", "Value");
        ViewBindingPlans.Apply(await Row(ui, 1, model), new Slots((rule, first)));
        BindingTargetRegistry.Register(new Overrides());
        ViewBindingPlans.Apply(await Row(ui, 2, model), new Slots((rule, second)));
        model.Write("Price", 10);
        ui.GetMvvm().FlushBindings();
        Assert.Equal(10, first.Value);
        Assert.Equal(20, second.Value);
    }
}
