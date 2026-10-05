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
    public sealed class ViewBindings : IDisposable
    {
        private readonly UINode _node;
        private readonly List<Action> _disconnect = new List<Action>();
        public ViewBindings(UINode node) => _node = node;
        public void Text(string path, string value) => _node.GetComponent<Text>(path).text = value;
        public void Button(string path, Func<Task> action)
        {
            var button = _node.GetComponent<Button>(path);
            UnityAction click = () => UnityClient.Instance.Track(action());
            button.onClick.AddListener(click);
            _disconnect.Add(() => button.onClick.RemoveListener(click));
        }
        public void Listen(Action refresh)
        {
            UnityClient.Instance.Store.Changed += refresh;
            _disconnect.Add(() => UnityClient.Instance.Store.Changed -= refresh);
        }
        public void Dropdown(string path, UnityAction<int> changed)
        {
            var dropdown = _node.GetComponent<Dropdown>(path);
            dropdown.onValueChanged.AddListener(changed);
            _disconnect.Add(() => dropdown.onValueChanged.RemoveListener(changed));
        }
        public void Toggle(string path, UnityAction<bool> changed)
        {
            var toggle = _node.GetComponent<Toggle>(path);
            toggle.onValueChanged.AddListener(changed);
            _disconnect.Add(() => toggle.onValueChanged.RemoveListener(changed));
        }
        public void Dispose() { foreach (var disconnect in _disconnect) disconnect(); }
    }
    public abstract class ClientView : UINode
    {
        protected UnityClient App => UnityClient.Instance;
        protected PlayerState Data => App.Store.State;
        protected ViewBindings Bindings;
        protected override async Task OnInitializeAsync(CancellationToken token)
        {
            Bindings = new ViewBindings(this);
            Bindings.Listen(Refresh);
            await InitializeViewAsync(token);
            Refresh();
        }
        protected virtual Task InitializeViewAsync(CancellationToken token) => Task.CompletedTask;
        protected virtual void Refresh() { }
        protected override Task OnDisposeAsync() { Bindings.Dispose(); return Task.CompletedTask; }
        protected override void OnActivated(StateChangeContext context) => Refresh();
    }
    public sealed class LoginView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        { Bindings.Button("Panel/Enter", App.EnterAsync); return Task.CompletedTask; }
    }
    public sealed class LobbyView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            foreach (var route in new[] { "Inventory", "Characters", "Shop", "Team", "Quests", "Settings" })
            { var name = route; Bindings.Button("Entries/" + name, () => App.OpenAsync(name)); }
            var hero = this.GetComponent<RectTransform>("Hero");
            var position = hero.anchoredPosition; var scale = hero.localScale;
            var groups = this.GetRect().GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g => g, g => g.alpha);
            Transition = Transitions.Select(c => c.Phase == TransitionPhase.PushEnter
                ? UnityTransitions.Timeline("Transitions/Enter", "Transitions/Exit", root =>
                { hero.anchoredPosition = position; hero.localScale = scale; foreach (var pair in groups) pair.Key.alpha = pair.Value; })
                : UnityTransitions.Fade(.2));
            return Task.CompletedTask;
        }
        protected override void Refresh() => Bindings.Text("Welcome/Text", "晨光已至，旅行者\n\n同行者正在等待下一次远征。\n金币 " + Data.Gold.ToString("N0") + " · 已购 " + Data.Purchases + " 次");
    }
    public sealed class InventoryView : ClientView
    {
        private ClientInventoryVm _inventory;
        public TabContainerNode Tabs { get; private set; }
        protected override async Task InitializeViewAsync(CancellationToken token)
        {
            Tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", this.GetComponent<RectTransform>("Pages"), token);
            foreach (var name in new[] { "装备", "材料" })
            {
                await Tabs.BindAsync(name, new UINode { Transition = UnityTransitions.Fade(.2) }, this.GetComponent<RectTransform>("Pages/" + name), token);
                var item = name;
                Bindings.Button("Tabs/" + name, () => Tabs.CurrentName == item ? Task.CompletedTask : Tabs.SwitchAsync(item));
            }
            await Tabs.SwitchAsync("装备", false, token);
            _inventory = this.CreateViewModel<ClientInventoryVm>("Inventory");
            var mount = this.GetComponent<RectTransform>("Pages/装备");
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
    public sealed class CharactersView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            for (int i = 0; i < 6; i++) { var id = i; Bindings.Button("Cards/C" + i, () => App.OpenHeroAsync(id)); }
            return Task.CompletedTask;
        }
        protected override void Refresh()
        { for (int i = 0; i < 6; i++) Bindings.Text("Cards/C" + i + "/Text", ClientStore.Heroes[i] + "\nLv." + Data.Levels[i]); }
    }
    public sealed class ItemView : UINode<int>
    {
        private ViewBindings _bindings;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this);
            var vm = this.CreateViewModel<ClientItemVm>("Item"); vm.Initialize(UnityClient.Instance.Store, Parameters);
            this.Bind("Item.Name", this.GetComponent<Text>("Panel/Title").TextTarget());
            this.Bind("Item.Description", this.GetComponent<Text>("Panel/Text").TextTarget());
            _bindings.Button("Panel/Compare", () => UnityClient.Instance.CompareAsync(Parameters));
            _bindings.Button("Panel/Back", UnityClient.Instance.BackAsync);
            return Task.CompletedTask;
        }
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
        public void RefreshComparison() => this.GetComponent<Button>("Panel/Compare").interactable = UnityClient.Instance.Windows.Children.Count < 2;
    }
    public sealed class HeroView : UINode<int>
    {
        private ViewBindings _bindings;
        private UnityClient App => UnityClient.Instance;
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            _bindings = new ViewBindings(this); _bindings.Listen(Refresh);
            var portraits = this.GetRect().Find("Portraits");
            for (int i = 0; i < 6; i++) portraits.GetChild(i).gameObject.SetActive(i == Parameters);
            _bindings.Button("Panel/Train", async () => { App.Store.Train(Parameters); await App.RewardAsync("培养完成\n" + ClientStore.Heroes[Parameters] + " Lv." + App.Store.State.Levels[Parameters]); });
            _bindings.Button("Panel/Equip", EquipAsync);
            _bindings.Button("Panel/Unequip", () => { App.Store.Equip(Parameters, -1); return Task.CompletedTask; });
            _bindings.Dropdown("Panel/Equipment", _ => Refresh());
            Refresh(); return Task.CompletedTask;
        }
        private async Task EquipAsync()
        {
            var selected = this.GetComponent<Dropdown>("Panel/Equipment").value;
            if (await App.AskAsync("为" + ClientStore.Heroes[Parameters] + "装备" + ClientStore.Items[selected] + "？")) App.Store.Equip(Parameters, selected);
        }
        private void Refresh()
        {
            var s = App.Store.State; var eq = s.Equipment[Parameters];
            _bindings.Text("Panel/Title", ClientStore.Heroes[Parameters] + " · Lv." + s.Levels[Parameters]);
            _bindings.Text("Panel/Text", "攻击 " + (s.Levels[Parameters] * 20 + (eq < 0 ? 0 : 80 + eq * 15)) + "\n\n装备：" + (eq < 0 ? "无" : ClientStore.Items[eq]));
            this.GetComponent<Button>("Panel/Train").interactable = s.Gold >= 300 && s.Materials >= 2;
            this.GetComponent<Button>("Panel/Unequip").interactable = eq >= 0;
            var selected = this.GetComponent<Dropdown>("Panel/Equipment").value;
            this.GetComponent<Button>("Panel/Equip").interactable = s.Inventory[selected] > s.Equipment.Count(i => i == selected) || eq == selected;
        }
        protected override void OnActivated(StateChangeContext context) => Refresh();
        protected override Task OnDisposeAsync() { _bindings.Dispose(); return Task.CompletedTask; }
    }
    public sealed class ShopView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        { for (int i = 0; i < 8; i++) { var id = i; Bindings.Button("Cards/C" + i, () => App.OpenPurchaseAsync(id)); } return Task.CompletedTask; }
        protected override void Refresh()
        { for (int i = 0; i < 8; i++) Bindings.Text("Cards/C" + i + "/Text", ClientStore.Items[i] + "\n" + ClientStore.Prices[i] + " 金币\n查看与购买"); }
    }
    public sealed class TeamView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        {
            for (int i = 0; i < 4; i++)
            {
                var slot = i;
                Bindings.Button("Slots/S" + i + "/Assign", () =>
                {
                    var hero = this.GetComponent<Dropdown>("Slots/S" + slot + "/Hero").value - 1;
                    App.Store.Change(s => s.Team[slot] = hero); return Task.CompletedTask;
                });
                Bindings.Dropdown("Slots/S" + i + "/Hero", _ => Refresh());
            }
            return Task.CompletedTask;
        }
        protected override void Refresh()
        {
            for (int i = 0; i < 4; i++)
            {
                var hero = Data.Team[i]; var choice = this.GetComponent<Dropdown>("Slots/S" + i + "/Hero").value - 1;
                Bindings.Text("Slots/S" + i + "/Text", "位置 " + (i + 1) + "\n" + (hero < 0 ? "空位" : ClientStore.Heroes[hero]));
                this.GetComponent<Button>("Slots/S" + i + "/Assign").interactable = choice < 0 || !Data.Team.Contains(choice) || hero == choice;
            }
        }
    }
    public sealed class QuestsView : ClientView
    {
        protected override Task InitializeViewAsync(CancellationToken token)
        { Bindings.Button("Panel/Claim", async () => { App.Store.ClaimQuest(); await App.RewardAsync("任务奖励\n金币 +800\n经验手记 +4"); }); return Task.CompletedTask; }
        protected override void Refresh()
        {
            Bindings.Text("Panel/Text", "整装待发\n\n在商店完成一次购买\n进度 " + Math.Min(1, Data.Purchases) + "/1\n\n奖励：800 金币、4 份经验手记" + (Data.QuestClaimed ? "\n已领取" : ""));
            this.GetComponent<Button>("Panel/Claim").interactable = Data.Purchases >= 1 && !Data.QuestClaimed;
        }
    }
}
