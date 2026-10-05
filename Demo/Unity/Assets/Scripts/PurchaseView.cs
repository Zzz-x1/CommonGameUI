using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
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
public sealed class PurchaseView : EngineUI.UINode<int>
    {
        private ViewBindings _bindings;
        private UnityClient App => UnityClient.Instance;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this); _bindings.Listen(Refresh);
            _bindings.Text("Panel/Title", ClientStore.Items[Parameters]);
            var purchase = this.CreateViewModel<ClientPurchaseVm>("Purchase"); purchase.Initialize(App, Parameters, () => App.Modals.CloseAsync(this));
            this.BindCommand("Purchase.Close", this.GetControl<Button>("Panel/Close").ClickTarget());
            this.BindCommand("Purchase.Buy", this.GetControl<Button>("Panel/Buy").ClickTarget());
            _bindings.Dropdown("Panel/Quantity", _ => Refresh());
            DismissRequested = _ => App.Modals.CloseAsync(this);
            Refresh(); return Task.CompletedTask;
        }
        private int Quantity => this.GetControl<Dropdown>("Panel/Quantity").value + 1;
        private void Refresh()
        {
            this.GetViewModel<ClientPurchaseVm>("Purchase").Quantity = Quantity;
            this.GetViewModel<ClientPurchaseVm>("Purchase").Gold = App.Store.State.Gold;
            var price = ClientStore.Prices[Parameters] * Quantity;
            _bindings.Text("Panel/Text", "数量 " + Quantity + " · 合计 " + price + " 金币\n\n购买将增加背包库存，推进整装待发任务。");
            this.GetControl<Button>("Panel/Buy").interactable = App.Store.State.Gold >= price;
        }
        protected override void OnActivated(StateChangeContext context) => Refresh();
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
