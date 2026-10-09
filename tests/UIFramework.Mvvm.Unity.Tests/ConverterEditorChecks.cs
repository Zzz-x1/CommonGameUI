using Button = UnityEngine.UI.Button;
using System;
using System.IO;
using System.Linq;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UIFramework.Mvvm.EditorUnity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
namespace NativeMvvm.Tests
{
    public static class ConverterEditorChecks
    {
        public static void Run()
        {
            Directory.CreateDirectory("Assets/ConverterChecks");
            var vm = ScriptableObject.CreateInstance<ConverterVm>();
            AssetDatabase.CreateAsset(vm, "Assets/ConverterChecks/Player.asset");
            Directory.CreateDirectory("Assets/Resources/UIFramework/Mvvm");
            var catalog = ScriptableObject.CreateInstance<GlobalViewModelCatalog>(); catalog.ViewModels.Add(vm);
            const string catalogPath = "Assets/Resources/UIFramework/Mvvm/ConverterChecks.asset";
            AssetDatabase.CreateAsset(catalog, catalogPath); AssetDatabase.SaveAssets();
            var root = new GameObject("Window", typeof(RectTransform), typeof(MvvmBindings));
            var child = new GameObject("Gold", typeof(RectTransform), typeof(Text)); child.transform.SetParent(root.transform, false);
            var label = child.GetComponent<Text>(); label.text = "before preview";
            var view = root.GetComponent<MvvmBindings>();
            view.Bindings.Add(new ViewBindingData { IsGlobal = true, Target = label, Source = "Player.Gold", Converter = "FormatNumber" });
            MvvmEditorWindow.Inspect(view, label);
            var window = Resources.FindObjectsOfTypeAll<MvvmEditorWindow>().Single();
            var input = window.rootVisualElement.Query<DropdownField>().ToList().Single(f => f.label == "转换器输入");
            input.value = ConverterInput.Constant.ToString();
            var constant = window.rootVisualElement.Query<TextField>().ToList().Single(f => f.label == "输入常量");
            constant.value = "125";
            Undo.FlushUndoRecordObjects();
            Check(view.Bindings[0].ConverterInput == ConverterInput.Constant && view.Bindings[0].ConverterConstant == "125", "GUI did not save converter input");
            using (var preview = new BindingPreview(view)) Check(label.text == "125", "preview did not use constant converter input");
            Check(label.text == "before preview" && vm.Gold == 10, "preview did not restore or modified asset");
            var group = new GameObject("Group", typeof(RectTransform)); group.transform.SetParent(root.transform, false);
            child.transform.SetParent(group.transform, false); child.name = "RenamedGold";
            Check(view.Bindings[0].Target == label, "target reference after rename and reparent");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/ConverterChecks/Window.prefab");
            var loaded = PrefabUtility.LoadPrefabContents("Assets/ConverterChecks/Window.prefab");
            var saved = loaded.GetComponent<MvvmBindings>().Bindings[0];
            Check(saved.Converter == "FormatNumber" && saved.ConverterInput == ConverterInput.Constant && saved.ConverterConstant == "125", "converter configuration did not survive Prefab reload");
            Check(saved.Target == loaded.GetComponentInChildren<Text>(), "target reference did not remap to Prefab instance");
            var loadedView = loaded.GetComponent<MvvmBindings>();
            using (var preview = new BindingPreview(loadedView)) Check(loaded.GetComponentInChildren<Text>().text == "125", "Manual asset sample preview");
            Check(loadedView.Bindings[0].IsGlobal && vm.Gold == 10, "Manual preview replaced original asset");
            PrefabUtility.UnloadPrefabContents(loaded);
            Undo.PerformUndo();
            Check(view.Bindings[0].ConverterInput == ConverterInput.ViewModel && view.Bindings[0].ConverterConstant == "", "Undo did not restore converter settings");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ConverterChecks/Window.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var instanceView = instance.GetComponent<MvvmBindings>();
            Undo.IncrementCurrentGroup();
            using (var serialized = new SerializedObject(instanceView))
            { serialized.FindProperty("Bindings.Array.data[0].ConverterConstant").stringValue = "300"; serialized.ApplyModifiedProperties(); }
            Check(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), "serialized converter did not create Prefab Override");
            Undo.PerformUndo(); Check(instanceView.Bindings[0].ConverterConstant == "125", "Prefab Override Undo failed");
            UnityEngine.Object.DestroyImmediate(instance);
            var presentation = root.AddComponent<TargetPresentation>();
            view.Bindings.Add(new ViewBindingData { IsGlobal = true, Target = presentation, Member = "Delay", Source = "Player.Gold", Converter = "IntToFloat" });
            var buy = new GameObject("Buy", typeof(RectTransform), typeof(Button)); buy.transform.SetParent(root.transform, false);
            view.Bindings.Add(new ViewBindingData { IsGlobal = true, Kind = ViewBindingKind.Command, Target = buy.GetComponent<Button>(), Source = "Player.Purchase", ParameterMode = CommandParameterMode.Constant, ParameterConstant = "2" });
            MvvmEditorWindow.Inspect(view, presentation);
            Check(window.rootVisualElement.Query<DropdownField>().ToList().Single(f => f.label == "目标属性").choices.Contains("Delay"), "custom target is absent from GUI");
            MvvmEditorWindow.Inspect(view, buy.GetComponent<Button>());
            Check(window.rootVisualElement.Query<TextField>().ToList().Single(f => f.label == "参数常量").value == "2", "command parameter GUI");
            var items = new GameObject("Items", typeof(RectTransform)); items.transform.SetParent(root.transform, false);
            var item = new GameObject("ItemTemplate", typeof(RectTransform), typeof(Text), typeof(MvvmBindings)); item.SetActive(false);
            item.GetComponent<MvvmBindings>().Bindings.Add(new ViewBindingData { Target = item.GetComponent<Text>(), Source = "Player.Gold", Converter = "FormatNumber" });
            vm.Items.Add(vm);
            view.Bindings.Add(new ViewBindingData { IsGlobal = true, Kind = ViewBindingKind.Collection, Target = items.GetComponent<RectTransform>(), Source = "Player.Items", ItemPrefab = item, OneTimeCollection = true, ItemSources = new System.Collections.Generic.List<CollectionSourceMapping> { new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" } } });
            using (var preview = new BindingPreview(view))
            { Check(presentation.Delay == 10, "custom target preview"); Check(items.transform.childCount == 1 && items.transform.GetChild(0).GetComponent<Text>().text == "10", "mapped item preview"); buy.GetComponent<Button>().onClick.Invoke(); Check(vm.Gold == 10, "preview executed parameter command"); }
            Check(presentation.Delay == 7 && items.transform.childCount == 0, "custom target / item preview restore");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/ConverterChecks/Extended.prefab");
            var extended = PrefabUtility.LoadPrefabContents("Assets/ConverterChecks/Extended.prefab");
            Check(extended.GetComponent<MvvmBindings>().Bindings.Last().ItemSources.Single().ItemSource == "Player", "mapped sources Prefab reload");
            Check(extended.GetComponent<MvvmBindings>().Bindings.Single(b => b.Kind == ViewBindingKind.Command).ParameterConstant == "2", "command constant Prefab reload");
            PrefabUtility.UnloadPrefabContents(extended); vm.Items.Clear(); UnityEngine.Object.DestroyImmediate(item);
            AssetDatabase.DeleteAsset(catalogPath); window.Close(); UnityEngine.Object.DestroyImmediate(root);
            File.WriteAllText("converter-checks.txt", "Passed: GUI constant configuration, preview, restoration, source asset, Prefab reload, target reference, Undo");
        }
        private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    }
}
