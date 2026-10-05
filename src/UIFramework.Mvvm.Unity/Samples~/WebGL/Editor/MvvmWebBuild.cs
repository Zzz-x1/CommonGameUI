using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MvvmWebSample.Editor
{
    public static class MvvmWebBuild
    {
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); new GameObject("MVVM Client", typeof(MvvmWebClient));
            EditorSceneManager.SaveScene(scene, "Assets/Client.unity"); PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false; PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            var result = BuildPipeline.BuildPlayer(new[] { "Assets/Client.unity" }, "Build", BuildTarget.WebGL, BuildOptions.None);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new IOException("WebGL build failed: " + result.summary.result);
        }
    }
}
