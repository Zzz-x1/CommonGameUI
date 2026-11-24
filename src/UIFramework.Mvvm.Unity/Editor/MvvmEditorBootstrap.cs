using EngineMvvm = global::UIFramework.Mvvm.Unity;
using UnityEditor;
using UIFramework.Mvvm.Unity;
namespace UIFramework.Mvvm.EditorUnity
{
    internal static class MvvmEditorBootstrap
    {
        private static readonly MvvmFrameDriver Frames = new MvvmFrameDriver();

        [InitializeOnLoadMethod] private static void Initialize()
        {
            EngineMvvm.MvvmBootstrap.EditModeObserver = Frames.Attach;
            EditorApplication.update -= Frames.Tick;
            EditorApplication.update += Frames.Tick;
            EngineMvvm.MvvmBootstrap.Register();
        }
    }
}
