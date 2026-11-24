using EngineMvvm = global::UIFramework.Mvvm.Unity;
using UguiSlider = UnityEngine.UI.Slider;
using System;
using System.Linq;
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
        private static void Start() { var task = Verify(); EditorApplication.update += Observe; void Observe() { if (!task.IsCompleted) return; EditorApplication.update -= Observe; try { task.GetAwaiter().GetResult(); Debug.Log("MVVM EDITOR: Undo, Prefab override and preview checks passed"); EditorApplication.Exit(0); } catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); } } }
        private static async Task Verify()
        {
            var root = new GameObject("Preview", typeof(RectTransform), typeof(MvvmBindings)); var cfg = root.GetComponent<MvvmBindings>();
            var label = new GameObject("Gold", typeof(RectTransform), typeof(Text)); label.transform.SetParent(root.transform); label.GetComponent<Text>().text = "original";
            var slider = new GameObject("Price range", typeof(RectTransform), typeof(UguiSlider)).GetComponent<UguiSlider>();
            slider.transform.SetParent(root.transform); slider.maxValue = 200; slider.SetValueWithoutNotify(150);
            var template = ScriptableObject.CreateInstance<PreviewPlayerVm>(); template.Gold = 100;
            System.IO.Directory.CreateDirectory("Assets/Temp"); AssetDatabase.CreateAsset(template, "Assets/Temp/PreviewPlayer.asset");
            const string catalogPath = "Assets/Resources/UIFramework/Mvvm/EditorVerification.asset";
            System.IO.Directory.CreateDirectory("Assets/Resources/UIFramework/Mvvm");
            var catalog = ScriptableObject.CreateInstance<GlobalViewModelCatalog>(); catalog.ViewModels.Add(template);
            AssetDatabase.CreateAsset(catalog, catalogPath); AssetDatabase.SaveAssets();
            cfg.Bindings.Add(new ViewBindingData { Target = label.GetComponent<Text>(), Source = "PreviewPlayer.Gold", Converter = "FormatNumber" });
            cfg.Bindings.Add(new ViewBindingData { Target = slider, Member = nameof(UguiSlider.minValue), Source = "PreviewPlayer.Gold", Converter = "IntToFloat" });
            var window = EditorWindow.GetWindow<MvvmEditorWindow>(); window.CreateGUI(); window.rootVisualElement.Q<ObjectField>("view").value = cfg;
            window.rootVisualElement.Q<ListView>("bindings").selectedIndex = 1;
            var memberPicker = window.rootVisualElement.Q<ScrollView>("details").Query<DropdownField>().ToList().Single(field => field.label == "目标属性");
            Check(memberPicker.choices.Contains(nameof(UguiSlider.minValue)), "Slider project member missing from editor");
            window.rootVisualElement.Q<ListView>("bindings").selectedIndex = 0;
            var name = window.rootVisualElement.Q<ScrollView>("details").Query<TextField>().ToList().Find(field => field.label == "来源路径"); name.value = "PreviewPlayer.CanBuy";
            Check(cfg.Bindings[0].Source == "PreviewPlayer.CanBuy", "Binding GUI failed"); Undo.PerformUndo(); Check(cfg.Bindings[0].Source == "PreviewPlayer.Gold", "GUI Undo failed");
            var timing = window.rootVisualElement.Q<ScrollView>("details").Query<DropdownField>().ToList().Find(field => field.label == "刷新时机");
            Check(timing.value == nameof(BindingUpdateTiming.EndOfFrame), "Expected the frame-end default");
            timing.value = nameof(BindingUpdateTiming.Immediate);
            Check(cfg.Bindings[0].UpdateTiming == BindingUpdateTiming.Immediate, "Update timing was not serialized");
            Undo.PerformUndo();
            Check(cfg.Bindings[0].UpdateTiming == BindingUpdateTiming.EndOfFrame, "Update timing Undo failed");
            using (var preview = new BindingPreview(cfg))
            {
                Check(label.GetComponent<Text>().text == "100", "Preview failed");
                Check(slider.minValue == 100, "Slider project member preview failed");
                Check(template.Gold == 100, "Asset changed in preview");
            }
            await Task.Yield();
            Check(label.GetComponent<Text>().text == "original" && slider.minValue == 0, "Preview did not restore");
            using (var preview = new BindingPreview(cfg))
            { Check(label.GetComponent<Text>().text == "100", "Global catalog preview failed"); Check(template.Gold == 100, "Global preview changed asset"); }
            await Task.Yield(); Check(label.GetComponent<Text>().text == "original", "Global preview did not restore");

            var prefabPath = "Assets/Temp/MvvmVerification.prefab"; System.IO.Directory.CreateDirectory("Assets/Temp"); PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)); var instanceConfig = instance.GetComponent<MvvmBindings>();
            window.rootVisualElement.Q<ObjectField>("view").value = instanceConfig; window.rootVisualElement.Q<ListView>("bindings").selectedIndex = 0;
            window.rootVisualElement.Q<ScrollView>("details").Query<DropdownField>().ToList().Find(field => field.label == "刷新时机").value = nameof(BindingUpdateTiming.Immediate);
            Check(instanceConfig.Bindings[0].UpdateTiming == BindingUpdateTiming.Immediate &&
                cfg.Bindings[0].UpdateTiming == BindingUpdateTiming.EndOfFrame && PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), "Timing prefab override missing");
            AssetDatabase.DeleteAsset(catalogPath); window.Close(); UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(root); AssetDatabase.DeleteAsset("Assets/Temp/PreviewPlayer.asset"); AssetDatabase.DeleteAsset(prefabPath);
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
    public sealed class PreviewPlayerVm : GlobalViewModel
    { [SerializeField] private int gold; [FieldNotify] public int Gold { get => gold; set => gold = value; } }
}
