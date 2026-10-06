using System.Collections.Generic;
using UIFramework.Mvvm;
using UnityEngine;
[assembly: BindingTargetProvider(typeof(NativeMvvm.Tests.ProjectTargets))]
namespace NativeMvvm.Tests
{
    public sealed class TargetPresentation : MonoBehaviour { public float Delay { get; set; } = 7; }
    public sealed class ProjectTargets : IBindingTargetProvider
    {
        public IEnumerable<BindingTargetDefinition> GetTargets()
        { yield return BindingTargetDefinition.Create<TargetPresentation, float>(nameof(TargetPresentation.Delay), t => t.Delay, (t,v) => t.Delay = v); }
    }
}
