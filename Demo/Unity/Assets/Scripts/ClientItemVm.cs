using System;
using UIFramework.Mvvm;
namespace StarCourt.UnityDemo
{
    [ViewModel]
    public class ClientItemVm : IDisposable
    {
        private ClientStore _store;
        private int _id;
        [FieldNotify] public string Name { get; set; } = "";
        [FieldNotify] public string Description { get; set; } = "";
        public void Initialize(ClientStore store, int id) { _store = store; _id = id; Name = ClientStore.Items[id]; _store.Changed += Refresh; Refresh(); }
        private void Refresh() => Description = "攻击 +" + (80 + _id * 15) + "\n\n持有 " + _store.State.Inventory[_id] + " 份\n\n装备可在角色档案中更换。";
        public void Dispose() => _store.Changed -= Refresh;
    }
    [ViewModel]
    public class ClientPurchaseVm
    {
        private UnityClient _app;
        private int _id;
        [FieldNotify] public int Quantity { get; set; } = 1;
        [FieldNotify] public int Gold { get; set; }
        [FieldNotify(nameof(Quantity), nameof(Gold))] public bool CanBuy => Gold >= ClientStore.Prices[_id] * Quantity;
        public void Initialize(UnityClient app, int id) { _app = app; _id = id; }
        [Command(CanExecute = nameof(CanBuy))] public async System.Threading.Tasks.Task Buy()
        {
            int quantity = Quantity;
            if (!await _app.AskAsync("花费 " + ClientStore.Prices[_id] * quantity + " 金币购买？")) return;
            _app.Store.Buy(_id, quantity);
            await _app.RewardAsync(ClientStore.Items[_id] + " × " + quantity + "\n\n已保存至背包");
        }
    }
}
