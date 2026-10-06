using System;
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
    public sealed class ShopView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            var vm = this.CreateViewModel<ClientShopVm>("Shop"); vm.Open = id => App.OpenPurchaseAsync(id);
            this.SetViewModel("Player", App.PlayerVm);
            var mount = this.GetControl<RectTransform>("Cards");
            for (int i = mount.childCount - 1; i >= 0; i--) { var old = mount.GetChild(i); old.SetParent(null, false); UnityEngine.Object.Destroy(old.gameObject); }
            var target = new CollectionTarget(this, mount, null, factory: new ShopItemFactory(), sources: new[] {
                new CollectionSourceMapping { ParentSource = "Shop", ItemSource = "Shop" }, new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" } });
            target.Changed += () => { for (int i = 0; i < mount.childCount; i++) { var row = (RectTransform)mount.GetChild(i); row.name = "C" + i; row.anchoredPosition = new Vector2(i % 4 * 370, -(i / 4) * 220); } };
            this.BindCollection("Shop.Items", target);
            for (int i = 0; i < 8; i++) vm.Items.Add(new ClientInventoryItemVm { Id = i, Text = ClientStore.Items[i] + "\n" + ClientStore.Prices[i] + " 金币\n查看与购买" });
            return Task.CompletedTask;
        }
        private sealed class ShopItemFactory : ICollectionItemFactory
        {
            public Task<IUINode> CreateAsync(CollectionItemContext context, CancellationToken cancellationToken) =>
                context.LoadAsync("MVVM/InventoryRow", node => {
                    var root = ((UIFramework.Unity.UIElement)node.Element).Rect;
                    var config = root.GetComponent<MvvmBindings>();
                    foreach (var binding in config.Bindings) if (binding.Kind == ViewBindingKind.Command) { binding.Source = "Shop.Select"; binding.ParameterMode = CommandParameterMode.SourcePath; binding.ParameterPath = "Item.Id"; }
                    node.Bind("Player.Gold", BindingTargets.Property(root.GetComponent<Image>(), "color"), new BindingOptions { Convert = value => (int)value >= ClientStore.Prices[((ClientInventoryItemVm)context.Item).Id] ? Color.white : new Color(.7f, .7f, .7f) });
                }, cancellationToken);
        }
    }
}
