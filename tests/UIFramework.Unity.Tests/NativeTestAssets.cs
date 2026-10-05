using System.IO;
using UnityEditor;
using UnityEngine;
namespace UIFramework.Tests.Unity
{
    public static class NativeTestAssets
    {
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Resources/Tests");
            Window("Window", true); Window("InactiveWindow", false);
            Save(new GameObject("Dialog", typeof(RectTransform), typeof(NativeDialog)), "Dialog");
            Save(new GameObject("Parameter", typeof(RectTransform), typeof(NativeParameterWindow)), "Parameter");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        private static void Window(string name, bool active)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(NativeWindow));
            var button = new GameObject("Close", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(root.transform, false);
            ((RectTransform)button.transform).sizeDelta = new Vector2(140, 44);
            var serialized = new SerializedObject(root.GetComponent<NativeWindow>());
            serialized.FindProperty("closeButton").objectReferenceValue = button.GetComponent<UnityEngine.UI.Button>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(active); Save(root, name);
        }
        private static void Save(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Tests/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }
    }
}
