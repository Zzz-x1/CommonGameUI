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
        [UnityTest] public IEnumerator NativeCollectionRowsInitializeAsynchronously() => Wait(CollectionRun());
        [UnityTest] public IEnumerator DefaultCatalogRegistersWithoutProjectInitialization() => Wait(DefaultCatalogRun());
        private static async Task DefaultCatalogRun()
        {
            const string folder = "Assets/Resources/UIFramework/Mvvm/ProviderVerification";
            var createdRoot = !System.IO.Directory.Exists("Assets/Resources/UIFramework");
            System.IO.Directory.CreateDirectory(folder);
            var template = ScriptableObject.CreateInstance<NativePlayerVm>(); template.Gold = 77;
            UnityEditor.AssetDatabase.CreateAsset(template, folder + "/Player.asset");
            var catalog = ScriptableObject.CreateInstance<GlobalViewModelCatalog>(); catalog.ViewModels.Add(template);
            UnityEditor.AssetDatabase.CreateAsset(catalog, folder + "/Catalog.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            var events = new GameObject("Catalog Events", typeof(EventSystem));
            var canvas = new GameObject("Catalog Canvas", typeof(Canvas));
            UIContext ui = null;
            try
            {
                ui = EngineUI.UI.Create(canvas.GetComponent<Canvas>(), events.GetComponent<EventSystem>());
                var vm = ui.GetGlobalViewModel<NativePlayerVm>(template.name);
                Assert.That(vm, Is.Not.SameAs(template)); Assert.That(vm.Gold, Is.EqualTo(77));
                Assert.That(ui.GetGlobalViewModel<NativePlayerVm>(template.name), Is.SameAs(vm));
                vm.Gold = 1; Assert.That(template.Gold, Is.EqualTo(77));
                await ui.DisposeAsync(); ui = null;
            }
            finally
            {
                if (ui != null) await ui.DisposeAsync();
                UnityEngine.Object.Destroy(canvas); UnityEngine.Object.Destroy(events);
                UnityEditor.AssetDatabase.DeleteAsset(folder);
                if (createdRoot) UnityEditor.AssetDatabase.DeleteAsset("Assets/Resources/UIFramework");
            }
        }
        private sealed class TestLog : IUILog { public readonly List<Exception> Errors = new List<Exception>(); public void Error(string message) => new ConsoleUILog().Error(message);
        public void Exception(Exception error, string context) => Errors.Add(error); }
        private static async Task CollectionRun()
        {
            var events = new GameObject("Events", typeof(EventSystem)); var canvas = new GameObject("Canvas", typeof(Canvas));
            var log = new TestLog(); var ui = EngineUI.UI.Create(canvas.GetComponent<Canvas>(), events.GetComponent<EventSystem>(), logger: log);
            var root = new GameObject("Root", typeof(RectTransform)); root.transform.SetParent(canvas.transform, false);
            var owner = new UINode(); await owner.AttachToAsync(ui.Root, "Root", (RectTransform)root.transform);
            var player = new ClientShopVm(); owner.SetViewModel("Player", player);
            var template = new GameObject("Row", typeof(RectTransform), typeof(CollectionRowNode)); template.SetActive(false);
            var target = new CollectionTarget(owner, (RectTransform)root.transform, template, sources: new[] { new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" } });
            owner.BindCollection("Player.Items", target); var first = new ClientInventoryItemVm { Id = 1 }; var second = new ClientInventoryItemVm { Id = 2 };
            player.Items.Add(first); player.Items.Add(second); player.Items.Move(0, 1); await ui.WhenMvvmIdleAsync();
            var row = root.transform.GetChild(0).GetComponent<CollectionRowNode>(); Assert.That(row.ItemAtInitialize, Is.SameAs(second)); Assert.That(row.PlayerAtInitialize, Is.SameAs(player)); Assert.That(row.State, Is.EqualTo(NodeState.Ready));
            player.Items.RemoveAt(0); await ui.WhenMvvmIdleAsync(); Assert.That(root.transform.childCount, Is.EqualTo(1));
            player.Items.Add(second); await owner.DisposeAsync(); Assert.That(root.transform.childCount, Is.EqualTo(0)); await ui.DisposeAsync(); Assert.That(log.Errors.TrueForAll(error => error is OperationCanceledException), Is.True);
            UnityEngine.Object.Destroy(template); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(canvas); UnityEngine.Object.Destroy(events);
        }
        private static IEnumerator Wait(Task task) { while (!task.IsCompleted) yield return null; task.GetAwaiter().GetResult(); }
        private static async Task Run()
        {
            var template = ScriptableObject.CreateInstance<NativePlayerVm>(); template.name = "Player"; template.Gold = 100; template.Items.Add(1);
            var events = new GameObject("MVVM Events", typeof(EventSystem)); var canvas = new GameObject("MVVM Canvas", typeof(Canvas));
            var ui = EngineUI.UI.Create(canvas.GetComponent<Canvas>(), events.GetComponent<EventSystem>()); var runtime = new MvvmRuntime(); ui.ConfigureMvvm(runtime);
            var rect = new GameObject("View", typeof(RectTransform), typeof(MvvmBindings)); rect.transform.SetParent(canvas.transform, false);
            var label = new GameObject("Gold", typeof(RectTransform), typeof(Text)); label.transform.SetParent(rect.transform, false);
            var button = new GameObject("Buy", typeof(RectTransform), typeof(Button)); button.transform.SetParent(rect.transform, false);
            runtime.RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template }));
            var config = rect.GetComponent<MvvmBindings>();
            config.Bindings.Add(new ViewBindingData { Target = label.GetComponent<Text>(), Source = template.name + ".Gold", Converter = "FormatNumber" });
            config.Bindings.Add(new ViewBindingData { Target = button.GetComponent<Button>(), Source = template.name + ".Buy", Kind = ViewBindingKind.Command });
            var node = new UINode(); await node.AttachToAsync(ui.Root, "View", (RectTransform)rect.transform);
            var vm = node.GetViewModel<NativePlayerVm>(template.name); Assert.That(vm, Is.Not.SameAs(template)); Assert.That(label.GetComponent<Text>().text, Is.EqualTo("100"));
            button.GetComponent<Button>().onClick.Invoke(); Assert.That(vm.Gold, Is.EqualTo(90)); Assert.That(template.Gold, Is.EqualTo(100));
            var slider = new GameObject("Amount", typeof(RectTransform), typeof(Slider)); slider.transform.SetParent(rect.transform, false);
            slider.GetComponent<Slider>().maxValue = 200;
            node.Bind("Player.Gold", slider.GetComponent<Slider>().ValueTarget(), new BindingOptions { Mode = BindingMode.TwoWay, Convert = Converters.Find("IntToFloat"), ConvertBack = Converters.Find("FloatToInt") });
            node.PauseBinding(nameof(NativePlayerVm.Gold)); vm.Gold = 120; slider.GetComponent<Slider>().value = 15;
            Assert.That(vm.Gold, Is.EqualTo(120)); Assert.That(label.GetComponent<Text>().text, Is.EqualTo("90"));
            node.ResumeBinding("Player.Gold"); Assert.That(label.GetComponent<Text>().text, Is.EqualTo("120")); Assert.That(slider.GetComponent<Slider>().value, Is.EqualTo(120));
            vm.Items.Add(2); Assert.That(template.Items.Count, Is.EqualTo(1)); vm.Gold = 0; Assert.That(button.GetComponent<Button>().interactable, Is.False);
            Assert.That(runtime.GetGlobalViewModel<NativePlayerVm>(template.name), Is.SameAs(vm));
            var otherEvents = new GameObject("Other Events", typeof(EventSystem));
            var otherCanvas = new GameObject("Other Canvas", typeof(Canvas));
            var otherUi = EngineUI.UI.Create(otherCanvas.GetComponent<Canvas>(), otherEvents.GetComponent<EventSystem>());
            otherUi.GetMvvm().RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template }));
            var independent = otherUi.GetMvvm().GetGlobalViewModel<NativePlayerVm>(template.name);
            Assert.That(independent, Is.Not.SameAs(vm)); Assert.That(independent.Gold, Is.EqualTo(100));
            await otherUi.DisposeAsync();
            UnityEngine.Object.Destroy(otherCanvas); UnityEngine.Object.Destroy(otherEvents);
            await node.DisposeAsync(); vm.Gold = 50; Assert.That(label.GetComponent<Text>().text, Is.EqualTo("0"));
            await ui.DisposeAsync(); await runtime.DisposeAsync();
            UnityEngine.Object.Destroy(rect); UnityEngine.Object.Destroy(canvas); UnityEngine.Object.Destroy(events); UnityEngine.Object.Destroy(template);
        }
    }
}
