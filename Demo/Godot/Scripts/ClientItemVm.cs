using Godot;
using UIFramework.Mvvm;
namespace HierarchyDemo;
[ViewModel]
public class ClientItemVm : IDisposable
{
    private ClientStore _store = null!;
    private string _id = "";
    [FieldNotify] public string Name { get; set; } = "";
    [FieldNotify] public string Description { get; set; } = "";
    [FieldNotify] public string Count { get; set; } = "";
    [FieldNotify] public Texture2D Icon { get; set; } = null!;
    public void Initialize(ClientStore store, string id)
    { _store = store; SelectItem(id); _store.Changed += Refresh; }
    public void SelectItem(string id)
    { _id = id; var item = Catalog.Item(id); Name = item.Name; Description = item.Description; Icon = GD.Load<Texture2D>($"res://Assets/Items/{item.Icon}.svg"); Refresh(); }
    private void Refresh() { var item = Catalog.Item(_id); Count = $"持有 {_store.State.Inventory[_id]}  ·  {(item.Power > 0 ? $"属性 +{item.Power}" : "培养材料")}"; }
    public void Dispose() => _store.Changed -= Refresh;
}
[ViewModel]
public class ClientPurchaseVm
{
    private ClientStore _store = null!;
    private ModalArgs _purchase = null!;
    private Func<Task> _complete = null!;
    [FieldNotify] public bool CanBuy { get; set; }
    public void Initialize(ClientStore store, ModalArgs purchase, Func<Task> complete) { _complete = complete; _store = store; _purchase = purchase; CanBuy = store.State.Gold >= Catalog.Items[purchase.Id].Price * purchase.Quantity; }
    [Command(CanExecute = nameof(CanBuy))] public async Task Buy()
    {
        _store.Buy(Catalog.Items[_purchase.Id].Id, _purchase.Quantity, _purchase.Text != "");
        await _complete();
    }
}
