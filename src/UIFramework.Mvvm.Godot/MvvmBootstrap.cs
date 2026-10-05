using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework.Godot;
using System.Runtime.CompilerServices;
namespace UIFramework.Mvvm.Godot;
internal static class MvvmBootstrap
{
    // This source module installs the optional adapter before project code creates a Context.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() => MvvmModule.RegisterAdapter(typeof(EngineUI.UIHost), new ViewModelAssets(),
        node => node.Element is EngineUI.UIElement display ? display.Control.GetChildren().OfType<MvvmBindings>().SingleOrDefault() : null,
        runtime => MvvmObserver.Attach((SceneTree)Engine.GetMainLoop(), runtime));
#pragma warning restore CA2255
}
