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
        int changes = 0; target.Changed += () => changes++; control.Edit(7);
        Assert.Equal(1, changes); target.Dispose(); target.Dispose(); Assert.Equal(0, control.Listeners);
        control.Edit(8); Assert.Equal(1, changes);
        Assert.Throws<InvalidOperationException>(() => BindingTargetRegistry.Find(control.GetType(), "Display").CreateTarget(control, BindingMode.TwoWay));
    }
}
