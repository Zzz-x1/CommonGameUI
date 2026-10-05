using EngineMvvm = global::UIFramework.Mvvm.Unity;
using UnityEditor;
using UIFramework.Mvvm.Unity;
namespace UIFramework.Mvvm.EditorUnity
{
    internal static class MvvmEditorBootstrap
    {
        [InitializeOnLoadMethod] private static void Initialize()
        {
            EngineMvvm.MvvmBootstrap.EditModeObserver = runtime =>
            {
                EditorApplication.CallbackFunction observe = null;
                observe = () => { runtime.ObserveTasks(); if (runtime.IsDisposed) EditorApplication.update -= observe; };
                EditorApplication.update += observe;
            };
            EngineMvvm.MvvmBootstrap.Register();
        }
    }
}
