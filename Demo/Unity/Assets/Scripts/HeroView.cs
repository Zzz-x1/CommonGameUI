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
        private ClientHeroVm _hero;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this); _bindings.Listen(Refresh);
            _hero = this.CreateViewModel<ClientHeroVm>("Hero");
            this.Bind("Hero.Power", this.GetControl<Text>("Panel/Text").TextTarget(), new BindingOptions { Convert = value => Stats((int)value) });
            var portraits = this.GetRoot().Find("Portraits");
            for (int i = 0; i < 6; i++) portraits.GetChild(i).gameObject.SetActive(i == Parameters);
            _bindings.Button("Panel/Train", TrainAsync);
            _bindings.Button("Panel/Equip", EquipAsync);
            _bindings.Button("Panel/Unequip", () => { App.Store.Equip(Parameters, -1); return Task.CompletedTask; });
            _bindings.Dropdown("Panel/Equipment", _ => Refresh());
            Refresh(); return Task.CompletedTask;
        }
        private Task TrainAsync() => App.RunAsync(this, TrainCoreAsync);
        private async Task TrainCoreAsync()
        {
            var previous = _hero.Power;
            this.PauseBinding(nameof(ClientHeroVm.Power));
            App.Store.Train(Parameters);
            var duration = App.Store.State.FullMotion ? .55f : .1f;
            float elapsed = 0;
            while (elapsed < duration)
            {
                await ((EngineUI.UIHost)Context.Host).NextFrameAsync();
                Lifetime.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Min(1, elapsed / duration);
                this.GetControl<Text>("Panel/Text").text = Stats(Mathf.RoundToInt(previous + (_hero.Power - previous) * (1 - Mathf.Pow(1 - progress, 3))));
            }
            this.ResumeBinding(nameof(ClientHeroVm.Power));
            await App.RewardAsync("培养完成\n" + ClientStore.Heroes[Parameters] + " Lv." + App.Store.State.Levels[Parameters]);
        }
        private string Stats(int power)
        {
            var equipment = App.Store.State.Equipment[Parameters];
            return "攻击 " + power + "\n\n装备：" + (equipment < 0 ? "无" : ClientStore.Items[equipment]);
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
            _hero.Power = s.Levels[Parameters] * 20 + (eq < 0 ? 0 : 80 + eq * 15);
            this.GetControl<Button>("Panel/Train").interactable = s.Gold >= 300 && s.Materials >= 2;
            this.GetControl<Button>("Panel/Unequip").interactable = eq >= 0;
            var selected = this.GetControl<Dropdown>("Panel/Equipment").value;
            this.GetControl<Button>("Panel/Equip").interactable = s.Inventory[selected] > s.Equipment.Count(i => i == selected) || eq == selected;
        }
        protected override void OnActivated(StateChangeContext context) => Refresh();
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
}
