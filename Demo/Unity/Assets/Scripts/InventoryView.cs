using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
public sealed class InventoryView : ClientView
    {
        private ClientInventoryVm _inventory;
        public TabContainerNode Tabs { get; private set; }
        protected override async Task InitializeViewAsync(CancellationToken token)
        {
            Tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", this.GetControl<RectTransform>("Pages"), token);
            foreach (var name in new[] { "装备", "材料" })
            {
                await Tabs.BindAsync(name, new UINode { Transition = EngineUI.Transitions.Fade(.2) }, this.GetControl<RectTransform>("Pages/" + name), token);
                var item = name;
                Bindings.Button("Tabs/" + name, () => Tabs.CurrentName == item ? Task.CompletedTask : Tabs.SwitchAsync(item));
            }
            await Tabs.SwitchAsync("装备", false, token);
            _inventory = this.CreateViewModel<ClientInventoryVm>("Inventory");
            var mount = this.GetControl<RectTransform>("Pages/装备");
            foreach (Transform child in mount.Cast<Transform>().ToArray()) { child.gameObject.SetActive(false); child.SetParent(null); UnityEngine.Object.Destroy(child.gameObject); }
            this.BindCollection("Inventory.Items", new InventoryGridTarget(this, mount, Resources.Load<GameObject>("MVVM/InventoryRow")));
            for (int i = 0; i < 8; i++) { int id = i; _inventory.Items.Add(new ClientInventoryItemVm { Id = id, Open = () => App.OpenItemAsync(id) }); }
        }
        protected override void Refresh()
        {
            foreach (var row in _inventory.Items) row.Text = ClientStore.Items[row.Id] + "\n拥有 " + Data.Inventory[row.Id] + " · 攻击 +" + (80 + row.Id * 15);
            Bindings.Text("Pages/材料/Text", "经验手记 × " + Data.Materials + "\n\n角色每次培养消耗 2 份手记与 300 金币。");
        }
    }
}
