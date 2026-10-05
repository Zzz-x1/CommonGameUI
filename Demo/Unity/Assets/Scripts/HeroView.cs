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
public sealed class HeroView : EngineUI.UINode<int>
    {
        private ViewBindings _bindings;
        private UnityClient App => UnityClient.Instance;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this); _bindings.Listen(Refresh);
            var portraits = this.GetRoot().Find("Portraits");
            for (int i = 0; i < 6; i++) portraits.GetChild(i).gameObject.SetActive(i == Parameters);
            _bindings.Button("Panel/Train", async () => { App.Store.Train(Parameters); await App.RewardAsync("培养完成\n" + ClientStore.Heroes[Parameters] + " Lv." + App.Store.State.Levels[Parameters]); });
            _bindings.Button("Panel/Equip", EquipAsync);
            _bindings.Button("Panel/Unequip", () => { App.Store.Equip(Parameters, -1); return Task.CompletedTask; });
            _bindings.Dropdown("Panel/Equipment", _ => Refresh());
            Refresh(); return Task.CompletedTask;
        }
        private async Task EquipAsync()
        {
            var selected = this.GetControl<Dropdown>("Panel/Equipment").value;
            if (await App.AskAsync("为" + ClientStore.Heroes[Parameters] + "装备" + ClientStore.Items[selected] + "？")) App.Store.Equip(Parameters, selected);
        }
        private void Refresh()
        {
            var s = App.Store.State; var eq = s.Equipment[Parameters];
            _bindings.Text("Panel/Title", ClientStore.Heroes[Parameters] + " · Lv." + s.Levels[Parameters]);
            _bindings.Text("Panel/Text", "攻击 " + (s.Levels[Parameters] * 20 + (eq < 0 ? 0 : 80 + eq * 15)) + "\n\n装备：" + (eq < 0 ? "无" : ClientStore.Items[eq]));
            this.GetControl<Button>("Panel/Train").interactable = s.Gold >= 300 && s.Materials >= 2;
            this.GetControl<Button>("Panel/Unequip").interactable = eq >= 0;
            var selected = this.GetControl<Dropdown>("Panel/Equipment").value;
            this.GetControl<Button>("Panel/Equip").interactable = s.Inventory[selected] > s.Equipment.Count(i => i == selected) || eq == selected;
        }
        protected override void OnActivated(StateChangeContext context) => Refresh();
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
