using Godot;
using UIFramework.Mvvm;
[assembly: BindingTargetProvider(typeof(NativeMvvm.Tests.ProjectTargets))]
namespace NativeMvvm.Tests;
[Tool] public partial class TargetPresentation : Control { public float Delay { get; set; } = 7; }
public sealed class ProjectTargets : IBindingTargetProvider
{
    public IEnumerable<BindingTargetDefinition> GetTargets()
    { yield return BindingTargetDefinition.Create<TargetPresentation, float>(nameof(TargetPresentation.Delay), t => t.Delay, (t,v) => t.Delay = v); }
}
