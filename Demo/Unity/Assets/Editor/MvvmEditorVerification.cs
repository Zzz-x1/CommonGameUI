using EngineMvvm = global::UIFramework.Mvvm.Unity;
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UIFramework.Mvvm.EditorUnity;

namespace StarCourt.UnityDemo.Editor
{
    public static class MvvmEditorVerification
    {
        public static void Run() { EditorApplication.delayCall += Start; }
        private static void Start() { var task = Verify(); EditorApplication.update += Observe; void Observe() { if (!task.IsCompleted) return; EditorApplication.update -= Observe; task.GetAwaiter().GetResult(); Debug.Log("MVVM EDITOR: Undo, Prefab override and preview checks passed"); EditorApplication.Exit(0); } }
        private static async Task Verify()
        {
            var root = new GameObject("Preview", typeof(RectTransform), typeof(MvvmBindings)); var cfg = root.GetComponent<MvvmBindings>();
            var label = new GameObject("Gold", typeof(RectTransform), typeof(Text)); label.transform.SetParent(root.transform); label.GetComponent<Text>().text = "original";
            var template = ScriptableObject.CreateInstance<PreviewPlayerVm>(); template.Gold = 100;
            cfg.ViewModels.Add(new ViewModelSourceData { Name = "Player", Template = template, Creation = ViewModelCreation.SharedAsset });
            cfg.Bindings.Add(new ViewBindingData { Target = label.GetComponent<Text>(), Source = "Player.Gold", Converter = "FormatNumber" });
            var window = EditorWindow.GetWindow<MvvmEditorWindow>(); window.CreateGUI(); window.rootVisualElement.Q<ObjectField>("view").value = cfg;
            window.rootVisualElement.Q<ListView>("sources").selectedIndex = 0;
            var name = window.rootVisualElement.Q<ScrollView>("details").Q<TextField>(); name.value = "Renamed";
            Check(cfg.ViewModels[0].Name == "Renamed", "Source GUI failed"); Undo.PerformUndo(); Check(cfg.ViewModels[0].Name == "Player", "GUI Undo failed");
            using (var preview = new BindingPreview(cfg)) { Check(label.GetComponent<Text>().text == "100", "Preview failed"); Check(template.Gold == 100, "Asset changed in preview"); }
            await Task.Yield(); Check(label.GetComponent<Text>().text == "original", "Preview did not restore");
            var prefabPath = "Assets/Temp/MvvmVerification.prefab"; System.IO.Directory.CreateDirectory("Assets/Temp"); PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)); var instanceConfig = instance.GetComponent<MvvmBindings>();
            window.rootVisualElement.Q<ObjectField>("view").value = instanceConfig; window.rootVisualElement.Q<ListView>("sources").selectedIndex = 0;
            window.rootVisualElement.Q<ScrollView>("details").Q<TextField>().value = "Override";
            Check(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), "Prefab override missing");
            window.Close(); UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(template); AssetDatabase.DeleteAsset(prefabPath);
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
    [ViewModel]
    public sealed class PreviewPlayerVm : ScriptableObject
    { [SerializeField] private int gold; [FieldNotify] public int Gold { get => gold; set => gold = value; } }
}
