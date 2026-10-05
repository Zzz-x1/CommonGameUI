using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIFramework;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;

namespace MvvmWebSample
{
    [ViewModel] public class PlayerVm : IDisposable
    {
        [FieldNotify] public int Gold { get; set; }
        [FieldNotify(nameof(Gold))] public bool CanBuy => Gold >= 10;
        [FieldNotify] public PlayerVm Selected { get; set; }
        [FieldNotify] public ObservableCollection<PlayerVm> Items { get; set; } = new ObservableCollection<PlayerVm>();
        [Command(CanExecute = nameof(CanBuy))] public void Buy() => Gold -= 10;
        public Func<Task> CloseView { get; set; }
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
        [Command] public Task Close() => CloseView();
        [Command] public async Task Wait(CancellationToken token) { await Task.Yield(); token.ThrowIfCancellationRequested(); }
    }
    [ViewModel] public sealed class PlayerAsset : ScriptableObject
    { [SerializeField] private int gold; [FieldNotify] public int Gold { get => gold; set => gold = value; } }
    public static class TextConversions { [Converter] public static string Gold(int value) => "Gold " + value; }
    internal sealed class Views : IUIResourceLoader, IUIResource
    {
        public EngineUI.UIHost Host;
        public Task<IUIResource> LoadAsync(string path, CancellationToken token) => Task.FromResult<IUIResource>(this);
        public IUIElement Instantiate(IUIElement mount)
        { var go = new GameObject("Dialog", typeof(RectTransform), typeof(Confirm)); var rect = (RectTransform)go.transform; rect.SetParent(((EngineUI.UIElement)mount).Rect, false); return new EngineUI.UIElement(Host, rect, true); }
        public void Dispose() { }
    }
    public sealed class MvvmWebClient : MonoBehaviour
    {
        [DllImport("__Internal")] private static extern void MvvmReport(int passed, string message);
        private Task _work;
        private int _checks;
        private void Start() => _work = Run();
        private void Update()
        {
            if (!_work.IsCompleted) return; enabled = false;
            try { _work.GetAwaiter().GetResult(); MvvmReport(_checks, "passed"); }
            catch (Exception error) { Debug.LogException(error); MvvmReport(0, error.ToString()); }
        }
        private void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); _checks++; Debug.Log("MVVM CHECK " + _checks + ": " + message); }
        private async Task Run()
        {
            var canvas = new GameObject("Canvas", typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var events = new GameObject("Events", typeof(EventSystem)); var views = new Views(); var ui = EngineUI.UI.Create(canvas.GetComponent<Canvas>(), events.GetComponent<EventSystem>(), views); views.Host = (EngineUI.UIHost)ui.Host;
            var runtime = ui.GetMvvm(); var node = new UINode(); await node.AttachAsync(ui.Root, "Bindings");
            var vm = node.CreateViewModel<PlayerVm>("Player"); vm.Gold = 100; var label = new GameObject("Gold", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); label.transform.SetParent(canvas.transform);
            node.Bind("Player.Gold", BindingTargets.Property(label, "text"), new BindingOptions { Convert = Converters.Find("MvvmWebSample.TextConversions.Gold") });
            Check(label.text == "Gold 100", "Woven getter/converter"); vm.Gold = 80; Check(label.text == "Gold 80", "Notification");
            var button = new GameObject("Buy", typeof(RectTransform), typeof(Button)).GetComponent<Button>(); node.BindCommand("Player.Buy", button.ClickTarget()); button.onClick.Invoke(); Check(vm.Gold == 70, "Command dispatch"); vm.Gold = 0; Check(!button.interactable, "Computed CanExecute");
            var slider = new GameObject("Amount", typeof(RectTransform), typeof(Slider)).GetComponent<Slider>(); slider.maxValue = 100;
            node.Bind("Player.Gold", slider.ValueTarget(), new BindingOptions { Mode = BindingMode.TwoWay, Convert = Converters.Find("IntToFloat"), ConvertBack = Converters.Find("FloatToInt") }); slider.value = 25; Check(vm.Gold == 25, "Two-way write");
            node.PauseBinding(nameof(PlayerVm.Gold)); vm.Gold = 55; slider.value = 8;
            Check(vm.Gold == 55 && label.text == "Gold 25", "Paused property group blocks both directions");
            node.ResumeBinding("Player.Gold"); Check(label.text == "Gold 55" && slider.value == 55, "Resume latest VM value");
            var nested = new GameObject("Selected", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); vm.Selected = new PlayerVm { Gold = 3 };
            node.Bind("Player.Selected.Gold", BindingTargets.Property(nested, "text"), new BindingOptions { Convert = Converters.Find("FormatNumber"), HasNullValue = true, NullValue = "none" }); vm.Selected = new PlayerVm { Gold = 7 }; Check(nested.text == "7", "Nested replacement"); vm.Selected = null; Check(nested.text == "none", "Null presentation");
            var asset = ScriptableObject.CreateInstance<PlayerAsset>(); asset.Gold = 42; var clone = runtime.GetSharedViewModel(asset); Check(clone != asset && clone.Gold == 42, "Asset clone"); Check(ReferenceEquals(clone, runtime.GetSharedViewModel(asset)), "Shared identity"); clone.Gold = 9; Check(asset.Gold == 42, "Original unchanged");
            var described = ((IMvvmObject)(object)vm).Describe(); Check(Array.Exists(described, p => p.Name == "Gold" && p.Observable), "Generated metadata");
            await ((IMvvmObject)(object)vm).Execute("Wait", null, default); Check(true, "Async command");
            var mount = new GameObject("Dialogs", typeof(RectTransform)); mount.transform.SetParent(canvas.transform); ui.Register<Confirm>("Dialog");
            var dialogs = await ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", (RectTransform)mount.transform);
            var answer = dialogs.ShowDialogAsync<Confirm, bool>(playAnimation: false); ((Confirm)dialogs.Current).Complete(true);
            Check(await answer && dialogs.Current == null, "Deferred dialog result and cleanup");
            var window = new UINode(); await window.AttachAsync(ui.Root, "ClosingWindow");
            var closing = window.CreateViewModel<PlayerVm>("Dialog");
            closing.CloseView = async () =>
            {
                await window.DisposeAsync();
                Check(window.State == NodeState.Disposed && closing.Disposals == 0, "Command awaited its own window close");
                await Task.Yield(); Check(closing.Disposals == 0, "VM retained through command continuation");
            };
            var closeButton = new GameObject("Close", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            window.BindCommand("Dialog.Close", closeButton.ClickTarget()); closeButton.onClick.Invoke();
            await runtime.WhenIdleAsync(); Check(closing.Disposals == 1, "Deferred VM release observed");
            await ui.DisposeAsync(); UnityEngine.Object.Destroy(asset); Check(runtime.IsDisposed, "Cleanup");
        }
    }
}
