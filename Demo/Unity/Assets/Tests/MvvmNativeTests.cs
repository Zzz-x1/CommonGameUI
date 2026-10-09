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
    public sealed class NativePlayerVm : GlobalViewModel
    {
        public static Exception InitializationError;
        public static TaskCompletionSource<bool> DisposalGate;
        public static NativePlayerVm LastInitialized;
        public int InitializationCount { get; private set; }
        public int DisposalCount { get; private set; }
        protected override void OnInitialize()
        {
            InitializationCount++; LastInitialized = this;
            if (InitializationError != null) throw InitializationError;
        }
        protected override async ValueTask OnDisposeAsync()
        {
            DisposalCount++;
            if (DisposalGate != null) await DisposalGate.Task;
        }
        [SerializeField] private NativePlayerVm other;
        [FieldNotify] public NativePlayerVm Other => other;
        public void SetOther(NativePlayerVm value) => other = value;
        [SerializeField] private int gold;
        [SerializeField] private List<int> items = new List<int>();
        [FieldNotify] public int Gold { get => gold; set => gold = value; }
        [FieldNotify] public List<int> Items { get => items; set => items = value; }
        [FieldNotify(nameof(Gold))] public bool CanBuy => Gold >= 10;
        [Command(CanExecute = nameof(CanBuy))] public void Buy() => Gold -= 10;
    }
    public sealed class MvvmNativeTests
    {
        [UnityTest] public IEnumerator GlobalOwnershipAndLifecycle() => Wait(GlobalRulesRun());
        private static async Task GlobalRulesRun()
        {
            var events = new GameObject("Global Rules Events", typeof(EventSystem));
            var canvas = new GameObject("Global Rules Canvas", typeof(Canvas));
            var log = new TestLog();
            var ui = EngineUI.UI.Create(canvas.GetComponent<Canvas>(), events.GetComponent<EventSystem>(), logger: log);
            var other = EngineUI.UI.Create(canvas.GetComponent<Canvas>(), events.GetComponent<EventSystem>(), logger: log);
            var template = ScriptableObject.CreateInstance<NativePlayerVm>(); template.name = "Rules"; template.Gold = 40;
            var external = ScriptableObject.CreateInstance<NativePlayerVm>(); external.name = "Nested";
            template.SetOther(external); external.SetOther(template);
            var unregistered = ScriptableObject.CreateInstance<NativePlayerVm>();
            try
            {
                ui.GetMvvm().RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template, external }));
                other.GetMvvm().RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template, external }));
                var node = new UINode(); await node.AttachAsync(ui.Root, "RulesNode");
                Assert.Throws<InvalidOperationException>(() => node.CreateViewModel<NativePlayerVm>("Independent"));
                Assert.Throws<InvalidOperationException>(() => node.CreateViewModel<NativePlayerVm>("IndependentTemplate", template));
                ui.RegisterViewModelFactory<object>(() => external);
                Assert.Throws<InvalidOperationException>(() => node.CreateViewModel<object>("Factory"));
                Assert.Throws<InvalidOperationException>(() => node.SetViewModel("Template", template));
                Assert.Throws<InvalidOperationException>(() => node.SetViewModel("External", external));
                Assert.Throws<InvalidOperationException>(() => ui.GetMvvm().GetSharedViewModel(unregistered, new ViewModelAssets()));
                var vm = ui.GetGlobalViewModel<NativePlayerVm>("Rules");
                Assert.That(vm.Other, Is.SameAs(ui.GetGlobalViewModel<NativePlayerVm>("Nested")));
                Assert.That(vm.Other.Other, Is.SameAs(vm));
                Assert.That(template.Other, Is.SameAs(external));
                Assert.That(external.Other, Is.SameAs(template));
                Assert.That(vm.InitializationCount, Is.EqualTo(1)); Assert.That(template.InitializationCount, Is.Zero);
                node.SetViewModel("SharedAlias", vm); Assert.That(node.GetViewModel<NativePlayerVm>("SharedAlias"), Is.SameAs(vm));
                var foreign = other.GetGlobalViewModel<NativePlayerVm>("Rules");
                Assert.That(foreign, Is.Not.SameAs(vm));
                Assert.Throws<InvalidOperationException>(() => node.SetViewModel("Foreign", foreign));
                other.ShareMvvm(ui);
                Assert.That(other.GetGlobalViewModel<NativePlayerVm>("Rules"), Is.SameAs(vm));
                await other.DisposeAsync(); Assert.That(vm.DisposalCount, Is.Zero);
                await node.DisposeAsync(); Assert.That(vm.DisposalCount, Is.Zero); Assert.That(external, Is.Not.Null);
                Assert.That(ui.GetGlobalViewModel<NativePlayerVm>("Rules"), Is.SameAs(vm));
                NativePlayerVm.DisposalGate = new TaskCompletionSource<bool>();
                var cleanup = ui.DisposeAsync().AsTask(); Assert.That(cleanup.IsCompleted, Is.False);
                NativePlayerVm.DisposalGate.SetResult(true); await cleanup; NativePlayerVm.DisposalGate = null;
                Assert.That(vm.DisposalCount, Is.EqualTo(1));
                var failed = new MvvmRuntime(log);
                failed.RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template, external }));
                var error = new InvalidOperationException("Global initialization identity"); NativePlayerVm.InitializationError = error;
                Assert.That(Assert.Throws<InvalidOperationException>(() => failed.GetGlobalViewModel<NativePlayerVm>("Rules")), Is.SameAs(error));
                var partial = NativePlayerVm.LastInitialized; NativePlayerVm.InitializationError = null;
                await failed.DisposeAsync(); Assert.That(partial.DisposalCount, Is.EqualTo(1)); Assert.That(log.Errors, Does.Contain(error));
                var preview = new MvvmRuntime(log);
                preview.RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template, external }, preview: true));
                var sample = preview.GetGlobalViewModel<NativePlayerVm>("Rules"); await preview.DisposeAsync();
                Assert.That(sample.InitializationCount, Is.Zero); Assert.That(sample.DisposalCount, Is.Zero);
            }
            finally
            {
                NativePlayerVm.InitializationError = null; NativePlayerVm.DisposalGate?.TrySetResult(true); NativePlayerVm.DisposalGate = null;
                await ui.DisposeAsync(); await other.DisposeAsync();
                UnityEngine.Object.Destroy(template); UnityEngine.Object.Destroy(external); UnityEngine.Object.Destroy(unregistered);
                UnityEngine.Object.Destroy(canvas); UnityEngine.Object.Destroy(events);
            }
        }
        [UnityTest] public IEnumerator AssetCloneBindingsAndNodeCleanup() => Wait(Run());
        [UnityTest] public IEnumerator NativeCollectionRowsInitializeAsynchronously() => Wait(CollectionRun());
        [UnityTest] public IEnumerator DefaultCatalogRegistersWithoutProjectInitialization() => Wait(DefaultCatalogRun());
        private static async Task DefaultCatalogRun()
        {
            const string folder = "Assets/Resources/UIFramework/Mvvm/ProviderVerification";
            var createdRoot = !System.IO.Directory.Exists("Assets/Resources/UIFramework");
            System.IO.Directory.CreateDirectory(folder);
            var template = ScriptableObject.CreateInstance<NativePlayerVm>(); template.Gold = 77;
            UnityEditor.AssetDatabase.CreateAsset(template, folder + "/NativePlayer.asset");
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
            var template = ScriptableObject.CreateInstance<NativePlayerVm>(); template.name = "NativePlayer"; template.Gold = 100; template.Items.Add(1);
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
            var vm = node.GetViewModel<NativePlayerVm>(template.name); Assert.That(vm.InitializationCount, Is.EqualTo(1)); Assert.That(template.InitializationCount, Is.EqualTo(0)); Assert.That(vm, Is.Not.SameAs(template)); Assert.That(label.GetComponent<Text>().text, Is.EqualTo("100"));
            button.GetComponent<Button>().onClick.Invoke(); Assert.That(vm.Gold, Is.EqualTo(90)); Assert.That(template.Gold, Is.EqualTo(100));
            var slider = new GameObject("Amount", typeof(RectTransform), typeof(Slider)); slider.transform.SetParent(rect.transform, false);
            slider.GetComponent<Slider>().maxValue = 200;
            node.Bind(template.name + ".Gold", slider.GetComponent<Slider>().ValueTarget(), new BindingOptions { Mode = BindingMode.TwoWay, Convert = Converters.Find("IntToFloat"), ConvertBack = Converters.Find("FloatToInt") });
            node.PauseBinding(nameof(NativePlayerVm.Gold)); vm.Gold = 120; slider.GetComponent<Slider>().value = 15;
            Assert.That(vm.Gold, Is.EqualTo(120)); Assert.That(label.GetComponent<Text>().text, Is.EqualTo("90"));
            node.ResumeBinding(template.name + ".Gold"); Assert.That(label.GetComponent<Text>().text, Is.EqualTo("120")); Assert.That(slider.GetComponent<Slider>().value, Is.EqualTo(120));
            vm.Items.Add(2); Assert.That(template.Items.Count, Is.EqualTo(1)); vm.Gold = 0; Assert.That(button.GetComponent<Button>().interactable, Is.False);
            Assert.That(runtime.GetGlobalViewModel<NativePlayerVm>(template.name), Is.SameAs(vm));
            Assert.That(vm.InitializationCount, Is.EqualTo(1));
            var otherEvents = new GameObject("Other Events", typeof(EventSystem));
            var otherCanvas = new GameObject("Other Canvas", typeof(Canvas));
            var otherUi = EngineUI.UI.Create(otherCanvas.GetComponent<Canvas>(), otherEvents.GetComponent<EventSystem>());
            otherUi.GetMvvm().RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template }));
            var independent = otherUi.GetMvvm().GetGlobalViewModel<NativePlayerVm>(template.name);
            Assert.That(independent, Is.Not.SameAs(vm)); Assert.That(independent.Gold, Is.EqualTo(100));
            Assert.That(independent.InitializationCount, Is.EqualTo(1));
            await using (var previewRuntime = new MvvmRuntime())
            {
                previewRuntime.RegisterGlobalViewModelProvider(new GlobalViewModelProvider(new[] { template }, preview: true));
                Assert.That(previewRuntime.GetGlobalViewModel<NativePlayerVm>(template.name).InitializationCount, Is.EqualTo(0));
            }
            await otherUi.DisposeAsync();
            UnityEngine.Object.Destroy(otherCanvas); UnityEngine.Object.Destroy(otherEvents);
            await node.DisposeAsync(); vm.Gold = 50; Assert.That(label.GetComponent<Text>().text, Is.EqualTo("0"));
            await ui.DisposeAsync(); await runtime.DisposeAsync();
            UnityEngine.Object.Destroy(rect); UnityEngine.Object.Destroy(canvas); UnityEngine.Object.Destroy(events); UnityEngine.Object.Destroy(template);
        }
    }
}
