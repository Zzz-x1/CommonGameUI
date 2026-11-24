using Godot;
using UIFramework.Mvvm;
[assembly: BindingTargetProvider(typeof(NativeMvvm.Tests.ProjectTargets))]
[assembly: GlobalViewModelProvider(typeof(NativeMvvm.Tests.ShopGlobals))]
namespace NativeMvvm.Tests;
public sealed class ShopGlobals : IGlobalViewModelProvider
{
    public IReadOnlyDictionary<string, object> ViewModels { get; } = new Dictionary<string, object>
        { ["Shop"] = ResourceLoader.Load<ConverterVm>("res://Player.tres") };
    public IViewModelAssets Assets { get; } = new UIFramework.Mvvm.Godot.ViewModelAssets();
}
public static class ShopConverters
{
    [Converter] public static float Price(int gold) => gold * 2;
}
[Tool] public partial class TargetPresentation : Control { public float Delay { get; set; } = 7; }
public sealed class ProjectTargets : IBindingTargetProvider
{
    public IEnumerable<BindingTargetDefinition> GetTargets()
    { yield return BindingTargetDefinition.Create<TargetPresentation, float>(nameof(TargetPresentation.Delay), t => t.Delay, (t,v) => t.Delay = v); }
}
