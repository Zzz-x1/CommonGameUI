using UIFramework.Mvvm;
using Xunit;

[assembly: BindingTargetProvider(typeof(UIFramework.Mvvm.Tests.TestTargetProvider))]
namespace UIFramework.Mvvm.Tests;
public class TestControl { public int Value; public int Listeners; public event Action? Changed; public void Edit(int value) { Value = value; Changed?.Invoke(); } public Action Subscribe(Action callback) { Listeners++; Changed += callback; return () => { Listeners--; Changed -= callback; }; } }
public sealed class DerivedControl : TestControl { }
public sealed class TestTargetProvider : IBindingTargetProvider
{
    public IEnumerable<BindingTargetDefinition> GetTargets()
    {
        yield return BindingTargetDefinition.Create<TestControl, int>("Value", t => t.Value, (t,v) => t.Value = v, (t,a) => t.Subscribe(a));
        yield return BindingTargetDefinition.Create<TestControl, int>("Display", t => t.Value, (t,v) => t.Value = v);
        yield return BindingTargetDefinition.Create<DerivedControl, int>("Display", t => t.Value + 10, (t,v) => t.Value = v - 10);
    }
}
public sealed class BindingTargetTests
{
    private sealed class LateControl : TestControl { }
    private sealed class LateProvider : IBindingTargetProvider
    {
        public IEnumerable<BindingTargetDefinition> GetTargets()
        {
            yield return BindingTargetDefinition.Create<LateControl, int>("Value", t => t.Value + 1, (t,v) => t.Value = v - 1);
        }
    }
    [Fact] public void LaterRegistrationReplacesCachedInheritedDefinition()
    {
        Assert.Equal(typeof(TestControl), BindingTargetRegistry.Find(typeof(LateControl), "Value").TargetType);
        BindingTargetRegistry.Register(new LateProvider());
        Assert.Equal(typeof(LateControl), BindingTargetRegistry.Find(typeof(LateControl), "Value").TargetType);
    }
    [Fact] public void RowsShareDefinitionButReleaseOnlyTheirOwnSubscription()
    {
        var definition = BindingTargetRegistry.Find(typeof(TestControl), "Value");
        var rows = Enumerable.Range(0, 30).Select(_ => new TestControl()).ToArray();
        var targets = rows.Select(row => BindingTargetRegistry.Find(row.GetType(), "Value").CreateTarget(row)).ToArray();
        Assert.All(rows, row => Assert.Equal(0, row.Listeners));
        foreach (var target in targets) target.Changed += () => { };
        targets[0].Dispose();
        for (var i = 1; i < rows.Length; i++)
        {
            Assert.Same(definition, BindingTargetRegistry.Find(rows[i].GetType(), "Value"));
            targets[i].Write(i);
            Assert.Equal(i, rows[i].Value);
            Assert.Equal(1, rows[i].Listeners);
            targets[i].Dispose();
        }
        Assert.All(rows, row => Assert.Equal(0, row.Listeners));
    }

    [Fact] public void DiscoveryDoesNotSubscribeAndPrefersDerivedDefinition()
    {
        var control = new DerivedControl();
        Assert.Equal(2, BindingTargetRegistry.Members(control.GetType()).Count);
        Assert.Equal(0, control.Listeners);
        using var target = BindingTargetRegistry.Find(control.GetType(), "Display").CreateTarget(control);
        target.Write(42); Assert.Equal(32, control.Value); Assert.Equal(42, target.Read());
        Assert.Throws<MissingMemberException>(() => BindingTargetRegistry.Find(control.GetType(), "Missing"));
    }
    [Fact] public void NotificationAndUnsubscribeAreExplicit()
    {
        var control = new TestControl(); var target = BindingTargetRegistry.Find(control.GetType(), "Value").CreateTarget(control, BindingMode.TwoWay);
        Assert.Equal(0, control.Listeners);
        int changes = 0;
        void Changed() => changes++;
        target.Changed += Changed;
        target.Changed += Changed;
        Assert.Equal(1, control.Listeners);
        control.Edit(7);
        Assert.Equal(2, changes);
        target.Changed -= Changed;
        Assert.Equal(1, control.Listeners);
        target.Changed -= Changed;
        Assert.Equal(0, control.Listeners);
        target.Changed += Changed;
        target.Dispose(); target.Dispose(); Assert.Equal(0, control.Listeners);
        control.Edit(8); Assert.Equal(2, changes);
        Assert.Throws<InvalidOperationException>(() => BindingTargetRegistry.Find(control.GetType(), "Display").CreateTarget(control, BindingMode.TwoWay));
    }
}
