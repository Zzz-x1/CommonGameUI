using EngineUI = global::UIFramework.Unity;
using System.Threading;
using System.Threading.Tasks;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
    public sealed class PurchaseView : EngineUI.UINode<int>
    {
        private UnityClient App => UnityClient.Instance;

        protected override Task OnInitializeAsync(CancellationToken token)
        {
            this.GetControl<Text>("Panel/Title").text = ClientStore.Items[Parameters];
            var purchase = this.CreateViewModel<ClientPurchaseVm>("Purchase");
            purchase.Initialize(App, Parameters, () => App.Modals.CloseAsync(this));
            var quantityPicker = this.GetControl<Dropdown>("Panel/Quantity");
            this.BindTwoWay<int>("Purchase.Quantity",
                apply: quantity => quantityPicker.SetValueWithoutNotify(quantity - 1),
                subscribe: edited =>
                {
                    void SelectQuantity(int index) => edited(index + 1);
                    var changed = quantityPicker.onValueChanged;
                    changed.AddListener(SelectQuantity);
                    return () => changed.RemoveListener(SelectQuantity);
                });
            int unitPrice = ClientStore.Prices[Parameters];
            this.Bind("Purchase.Quantity", "Player.Gold", this.GetControl<Text>("Panel/Text").TextTarget(),
                (int quantity, int gold) =>
                {
                    int total = unitPrice * quantity;
                    string detail = gold >= total ? "购买将增加背包库存，推进整装待发任务。" : $"还差 {total - gold} 金币。";
                    return $"数量 {quantity} · 合计 {total} 金币\n\n{detail}";
                });
            this.BindCommand("Purchase.Close", this.GetControl<Button>("Panel/Close").ClickTarget());
            this.BindCommand("Purchase.Buy", this.GetControl<Button>("Panel/Buy").ClickTarget());
            DismissRequested = _ => App.Modals.CloseAsync(this);
            return Task.CompletedTask;
        }
    }
}
