using System.Collections.ObjectModel;
using UIFramework.Mvvm;
namespace HierarchyDemo;
[ViewModel] public class ClientShopVm
{
    [FieldNotify] public ObservableCollection<ClientInventoryItemVm> Equipment { get; set; } = new();
    [FieldNotify] public ObservableCollection<ClientInventoryItemVm> Materials { get; set; } = new();
    [FieldNotify] public ObservableCollection<ClientInventoryItemVm> Bundles { get; set; } = new();
    public Func<int, Task> Open { get; set; } = null!;
    [Command] public Task Select(int productId) => Open(productId);
}
