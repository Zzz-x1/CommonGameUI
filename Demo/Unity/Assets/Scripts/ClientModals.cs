using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
    public sealed class SettingsView : ClientView
    {
        public TabContainerNode Tabs { get; private set; }
        protected override async Task InitializeViewAsync(CancellationToken token)
        {
            Bindings.Button("Panel/Close", App.BackAsync);
            Bindings.Button("Panel/Pages/画面/Graphics", () => App.Modals.OpenAsync<GraphicsView>());
            Bindings.Button("Panel/Defaults", async () =>
            {
                if (await App.AskAsync("恢复默认画质与演出设置？")) App.Store.Change(s => { s.FullMotion = true; s.Quality = 2; });
            });
            Tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", this.GetComponent<RectTransform>("Panel/Pages"), token);
            foreach (var name in new[] { "画面", "演出" })
            {
                var item = name;
                await Tabs.BindAsync(name, new UINode { Transition = UnityTransitions.Fade(.18) }, this.GetComponent<RectTransform>("Panel/Pages/" + name), token);
                Bindings.Button("Panel/Tabs/" + name, () => Tabs.CurrentName == item ? Task.CompletedTask : Tabs.SwitchAsync(item));
            }
            var vm = this.CreateViewModel<ClientSettingsVm>("Settings"); vm.Initialize(App.Store);
            this.Bind("Settings.FullMotion", this.GetComponent<Toggle>("Panel/Pages/演出/Motion").ToggleTarget(), BindingMode.TwoWay);
            await Tabs.SwitchAsync("画面", false, token);
            DismissRequested = _ => App.Modals.CloseAsync(this);
        }
        protected override void Refresh()
        {
            this.GetComponent<Toggle>("Panel/Pages/演出/Motion").SetIsOnWithoutNotify(Data.FullMotion);
            Bindings.Text("Panel/Pages/画面/Text", "画质档位：" + new[] { "流畅", "均衡", "精致" }[Data.Quality] + "\n\n这是纯 UI 客户端，画质档位保存为本地偏好。");
        }
    }
    public sealed class GraphicsView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            Bindings.Dropdown("Panel/Quality", value => App.Store.Change(s => s.Quality = value));
            Bindings.Button("Panel/Close", App.BackAsync);
            DismissRequested = _ => App.Modals.CloseAsync(this);
            return Task.CompletedTask;
        }
        protected override void Refresh() => this.GetComponent<Dropdown>("Panel/Quality").SetValueWithoutNotify(Data.Quality);
    }
    public sealed class ConfirmView : DialogNode<bool>
    {
        private readonly string _message;
        private ViewBindings _bindings;
        public ConfirmView(string message) => _message = message;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            _bindings.Text("Panel/Text", _message);
            _bindings.Button("Panel/Yes", () => { Complete(true); return Task.CompletedTask; });
            _bindings.Button("Panel/No", () => { Complete(false); return Task.CompletedTask; });
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
    public sealed class PurchaseView : UINode<int>
    {
        private ViewBindings _bindings;
        private UnityClient App => UnityClient.Instance;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this); _bindings.Listen(Refresh);
            _bindings.Text("Panel/Title", ClientStore.Items[Parameters]);
            _bindings.Button("Panel/Close", App.BackAsync);
            var purchase = this.CreateViewModel<ClientPurchaseVm>("Purchase"); purchase.Initialize(App, Parameters);
            this.BindCommand("Purchase.Buy", this.GetComponent<Button>("Panel/Buy").ClickTarget());
            _bindings.Dropdown("Panel/Quantity", _ => Refresh());
            DismissRequested = _ => App.Modals.CloseAsync(this);
            Refresh(); return Task.CompletedTask;
        }
        private int Quantity => this.GetComponent<Dropdown>("Panel/Quantity").value + 1;
        private void Refresh()
        {
            this.GetViewModel<ClientPurchaseVm>("Purchase").Quantity = Quantity;
            this.GetViewModel<ClientPurchaseVm>("Purchase").Gold = App.Store.State.Gold;
            var price = ClientStore.Prices[Parameters] * Quantity;
            _bindings.Text("Panel/Text", "数量 " + Quantity + " · 合计 " + price + " 金币\n\n购买将增加背包库存，推进整装待发任务。");
            this.GetComponent<Button>("Panel/Buy").interactable = App.Store.State.Gold >= price;
        }
        protected override void OnActivated(StateChangeContext context) => Refresh();
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
    public sealed class RewardView : UINode<string>
    {
        private ViewBindings _bindings;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            _bindings.Text("Panel/Text", Parameters);
            _bindings.Button("Panel/Close", UnityClient.Instance.BackAsync);
            var rects = this.GetRect().GetComponentsInChildren<RectTransform>(true)
                .ToDictionary(r => r, r => (r.anchoredPosition, r.localScale, r.localRotation));
            var groups = this.GetRect().GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g => g, g => g.alpha);
            Transition = Transitions.Select(c => c.Entering && c.Source is PurchaseView && UnityClient.Instance.Store.State.FullMotion
                ? UnityTransitions.Animation(null, "RewardEnter", "RewardExit", root =>
                {
                    foreach (var pair in rects)
                    { pair.Key.anchoredPosition = pair.Value.anchoredPosition; pair.Key.localScale = pair.Value.localScale; pair.Key.localRotation = pair.Value.localRotation; }
                    foreach (var pair in groups) pair.Key.alpha = pair.Value;
                })
                : UnityTransitions.ScaleFade(.15, "Panel"));
            DismissRequested = _ => UnityClient.Instance.Modals.CloseAsync(this);
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
    public sealed class CompareArgs
    {
        public CompareArgs(int item, int position) { Item = item; Position = position; }
        public int Item { get; }
        public int Position { get; }
    }
    public sealed class CompareView : UINode<CompareArgs>
    {
        private ViewBindings _bindings;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            this.GetComponent<RectTransform>("Panel").anchoredPosition += Vector2.right * Parameters.Position * 370;
            _bindings.Text("Panel/Title", "装备对比 · " + (Parameters.Position + 1));
            _bindings.Text("Panel/Text", ClientStore.Items[Parameters.Item] + "\n\n攻击 +" + (80 + Parameters.Item * 15));
            _bindings.Button("Panel/Close", () => UnityClient.Instance.CloseComparisonAsync(this));
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
