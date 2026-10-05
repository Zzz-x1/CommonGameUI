using Godot;
using UIFramework.Godot;
using System.Runtime.CompilerServices;
namespace UIFramework.Mvvm.Godot;
internal static class GodotMvvmBootstrap
{
    // This source module installs the optional adapter before project code creates a Context.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() => MvvmModule.RegisterAdapter(typeof(GodotUIHost), new GodotViewModelAssets(),
        node => node.Element is GodotUIElement display ? display.Control.GetChildren().OfType<MvvmBindings>().SingleOrDefault() : null,
        runtime => GodotMvvmObserver.Attach((SceneTree)Engine.GetMainLoop(), runtime));
#pragma warning restore CA2255
}
