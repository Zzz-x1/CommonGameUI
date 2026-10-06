using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UIFramework.Mvvm;
namespace StarCourt.UnityDemo
{
    [ViewModel] public class ClientShopVm
    {
        [FieldNotify] public ObservableCollection<ClientInventoryItemVm> Items { get; set; } = new ObservableCollection<ClientInventoryItemVm>();
        public Func<int, Task> Open { get; set; }
        [Command] public Task Select(int productId) => Open(productId);
    }
}
