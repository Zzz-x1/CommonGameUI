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
            var root = new GameObject("Window", typeof(RectTransform), typeof(MvvmBindings));
            var child = new GameObject("Gold", typeof(RectTransform), typeof(Text)); child.transform.SetParent(root.transform, false);
            var label = child.GetComponent<Text>(); label.text = "before preview";
            var view = root.GetComponent<MvvmBindings>();
            view.ViewModels.Add(new ViewModelSourceData { Name = "Player", Template = vm });
            view.Bindings.Add(new ViewBindingData { Target = label, Source = "Player.Gold", Converter = "FormatNumber" });
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
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/ConverterChecks/Window.prefab");
            var loaded = PrefabUtility.LoadPrefabContents("Assets/ConverterChecks/Window.prefab");
            var saved = loaded.GetComponent<MvvmBindings>().Bindings[0];
            Check(saved.Converter == "FormatNumber" && saved.ConverterInput == ConverterInput.Constant && saved.ConverterConstant == "125", "converter configuration did not survive Prefab reload");
            Check(saved.Target == loaded.GetComponentInChildren<Text>(), "target reference did not remap to Prefab instance");
            PrefabUtility.UnloadPrefabContents(loaded);
            Undo.PerformUndo();
            Check(view.Bindings[0].ConverterInput == ConverterInput.ViewModel && view.Bindings[0].ConverterConstant == "", "Undo did not restore converter settings");
            window.Close(); UnityEngine.Object.DestroyImmediate(root);
            File.WriteAllText("converter-checks.txt", "Passed: GUI constant configuration, preview, restoration, source asset, Prefab reload, target reference, Undo");
        }
        private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    }
}
