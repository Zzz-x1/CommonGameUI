using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UIFramework;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;

namespace StarCourt.UnityDemo.Tests
{
    [ViewModel]
    public sealed class NativePlayerVm : ScriptableObject
    {
        [SerializeField] private int gold;
        [SerializeField] private List<int> items = new List<int>();
        [FieldNotify] public int Gold { get => gold; set => gold = value; }
        [FieldNotify] public List<int> Items { get => items; set => items = value; }
        [FieldNotify(nameof(Gold))] public bool CanBuy => Gold >= 10;
        [Command(CanExecute = nameof(CanBuy))] public void Buy() => Gold -= 10;
    }
    public sealed class MvvmNativeTests
    {
        [UnityTest] public IEnumerator AssetCloneBindingsAndNodeCleanup() => Wait(Run());
        private static IEnumerator Wait(Task task) { while (!task.IsCompleted) yield return null; task.GetAwaiter().GetResult(); }
        private static async Task Run()
        {
            var template = ScriptableObject.CreateInstance<NativePlayerVm>(); template.Gold = 100; template.Items.Add(1);
            var events = new GameObject("MVVM Events", typeof(EventSystem)); var canvas = new GameObject("MVVM Canvas", typeof(Canvas));
            var ui = EngineUI.UI.Create(canvas.GetComponent<Canvas>(), events.GetComponent<EventSystem>()); var runtime = new MvvmRuntime(); ui.ConfigureMvvm(runtime);
            var rect = new GameObject("View", typeof(RectTransform), typeof(MvvmBindings)); rect.transform.SetParent(canvas.transform, false);
            var label = new GameObject("Gold", typeof(RectTransform), typeof(Text)); label.transform.SetParent(rect.transform, false);
            var button = new GameObject("Buy", typeof(RectTransform), typeof(Button)); button.transform.SetParent(rect.transform, false);
            var config = rect.GetComponent<MvvmBindings>(); config.ViewModels.Add(new ViewModelSourceData { Name = "Player", Template = template, Creation = ViewModelCreation.SharedAsset });
            config.Bindings.Add(new ViewBindingData { Target = label.GetComponent<Text>(), Source = "Player.Gold", Converter = "FormatNumber" });
            config.Bindings.Add(new ViewBindingData { Target = button.GetComponent<Button>(), Source = "Player.Buy", Kind = ViewBindingKind.Command });
            var node = new UINode(); await node.AttachToAsync(ui.Root, "View", (RectTransform)rect.transform);
            var vm = node.GetViewModel<NativePlayerVm>("Player"); Assert.That(vm, Is.Not.SameAs(template)); Assert.That(label.GetComponent<Text>().text, Is.EqualTo("100"));
            button.GetComponent<Button>().onClick.Invoke(); Assert.That(vm.Gold, Is.EqualTo(90)); Assert.That(template.Gold, Is.EqualTo(100));
            vm.Items.Add(2); Assert.That(template.Items.Count, Is.EqualTo(1)); vm.Gold = 0; Assert.That(button.GetComponent<Button>().interactable, Is.False);
            await node.DisposeAsync(); vm.Gold = 50; Assert.That(label.GetComponent<Text>().text, Is.EqualTo("0"));
            await ui.DisposeAsync(); await runtime.DisposeAsync();
            UnityEngine.Object.Destroy(rect); UnityEngine.Object.Destroy(canvas); UnityEngine.Object.Destroy(events); UnityEngine.Object.Destroy(template);
        }
    }
}
