using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
namespace StarCourt.UnityDemo.Editor
{
    public static class TimelinePackageInstaller
    {
        private static AddRequest _request;
        public static void Install() { _request = Client.Add("com.unity.timeline@1.8.13"); EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (!_request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (_request.Status != StatusCode.Success) { Debug.LogError(_request.Error.message); EditorApplication.Exit(1); return; }
            Debug.Log("Timeline installed: " + _request.Result.version); EditorApplication.Exit(0);
        }
    }
}
