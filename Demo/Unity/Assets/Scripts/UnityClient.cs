using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarCourt.UnityDemo
{
    public sealed class UnityClient : MonoBehaviour
    {
        public Canvas Canvas;
        public EventSystem Events;
        public RectTransform PageMount, WindowMount, ModalMount;
        public Text Wallet;
        public GameObject Navigation;
        public string SaveFile = "unity-client-save.json";
        public static UnityClient Instance { get; private set; }
        public UIContext UI { get; private set; }
        public StackContainerNode Pages { get; private set; }
        public PanelContainerNode Windows { get; private set; }
        public ModalContainerNode Modals { get; private set; }
        public ClientStore Store { get; private set; }
        public LobbyView Home { get; private set; }
        public bool Busy { get; private set; }
        public Task Ready { get; private set; }
        public Task WhenIdleAsync() => Task.WhenAll(_tasks.ToArray());
        private readonly List<Task> _tasks = new List<Task>();
        private string _confirmation;
        private void Awake() { Instance = this; Application.runInBackground = true; }
        private void Start() { Ready = InitializeAsync(); Track(Ready); }
        public void Track(Task task) => _tasks.Add(task);
        private void Update()
        {
            for (int i = _tasks.Count - 1; i >= 0; i--)
                if (_tasks[i].IsCompleted)
                {
                    var completed = _tasks[i]; _tasks.RemoveAt(i);
                    completed.GetAwaiter().GetResult();
                }
            if (!Busy && Input.GetKeyDown(KeyCode.Escape)) Track(BackAsync());
        }
        private async Task InitializeAsync()
        {
            Store = new ClientStore(Path.Combine(Application.persistentDataPath, SaveFile));
            UI = UnityUI.Create(Canvas, Events);
            UI.Register<LoginView>("UI/Login"); UI.Register<LobbyView>("UI/Lobby");
            UI.Register<InventoryView>("UI/Inventory"); UI.Register<ItemView>("UI/Item");
            UI.Register<CharactersView>("UI/Characters"); UI.Register<HeroView>("UI/Hero");
            UI.Register<ShopView>("UI/Shop"); UI.Register<TeamView>("UI/Team");
            UI.Register<QuestsView>("UI/Quests"); UI.Register<SettingsView>("UI/Settings");
            UI.Register<GraphicsView>("UI/Graphics"); UI.Register<RewardView>("UI/Reward");
            UI.Register<CompareView>("UI/Compare", windowMode: WindowInstanceMode.Multiple);
            UI.Register<PurchaseView>("UI/Purchase");
            UI.Register(() => new ConfirmView(_confirmation), "UI/Confirm");
            Pages = await UI.Root.CreateContainerAsync<StackContainerNode>("Client", PageMount);
            Windows = await UI.Root.CreateContainerAsync<PanelContainerNode>("Comparison", WindowMount);
            Modals = await UI.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", ModalMount);
            Pages.ChildTransition = ClientTransitions.Page();
            Windows.ChildTransition = UnityTransitions.ScaleFade();
            Modals.ChildTransition = UnityTransitions.ScaleFade(targetPath: "Panel");
            Modals.ConfigureBackdrop(UnityBackdrops.Dim(Track), ModalBackdropMode.Shared);
            foreach (var button in Navigation.GetComponentsInChildren<Button>(true))
            {
                var route = button.name;
                button.onClick.AddListener(() => Track(OpenAsync(route)));
            }
            Navigation.SetActive(false);
            Store.Changed += Refresh;
            Refresh();
            await Pages.PushAsync<LoginView>();
        }
        private void Refresh() => Wallet.text = "金币 " + Store.State.Gold.ToString("N0") + "    经验手记 " + Store.State.Materials;
        private async Task RunAsync(Func<Task> action)
        {
            if (Busy) throw new InvalidOperationException("A client operation is still running.");
            Busy = true;
            var inputs = Navigation.GetComponent<CanvasGroup>();
            inputs.interactable = false; inputs.blocksRaycasts = false;
            try { await action(); }
            finally { inputs.interactable = true; inputs.blocksRaycasts = true; Busy = false; }
        }
        public Task EnterAsync() => RunAsync(async () =>
        {
            Home = await Pages.PushAsync<LobbyView>();
            Navigation.SetActive(true);
        });
        public Task OpenAsync(string route) => RunAsync(async () =>
        {
            if (route == "Settings") { await Modals.OpenAsync<SettingsView>(); return; }
            if (Windows.Children.Count > 0)
            {
                foreach (var window in Windows.Children.Reverse().ToArray()) await Windows.CloseAsync(window);
                ((ItemView)Pages.Current).RefreshComparison();
            }
            if (route == "Home") { if (Pages.Current != Home) await Pages.PopToAsync(Home); return; }
            if (Pages.Current.GetType().Name == route + "View") return;
            switch (route)
            {
                case "Inventory": await Pages.PushAsync<InventoryView>(); break;
                case "Characters": await Pages.PushAsync<CharactersView>(); break;
                case "Shop": await Pages.PushAsync<ShopView>(); break;
                case "Team": await Pages.PushAsync<TeamView>(); break;
                case "Quests": await Pages.PushAsync<QuestsView>(); break;
                default: throw new ArgumentOutOfRangeException(nameof(route), route, "Unknown route.");
            }
        });
        public Task RewardAsync(string text) => Modals.OpenAsync<RewardView>(text);
        public Task OpenItemAsync(int id) => RunAsync(async () => { await Pages.PushAsync<ItemView>(id); });
        public Task OpenHeroAsync(int id) => RunAsync(async () => { await Pages.PushAsync<HeroView>(id); });
        public Task OpenPurchaseAsync(int id) => RunAsync(async () => { await Modals.OpenAsync<PurchaseView>(id); });
        public Task CompareAsync(int id) => RunAsync(async () =>
        {
            await Windows.OpenAsync<CompareView>(new CompareArgs(id, Windows.Children.Count));
            ((ItemView)Pages.Current).RefreshComparison();
        });
        public Task CloseComparisonAsync(CompareView view) => RunAsync(async () =>
        {
            await Windows.CloseAsync(view);
            ((ItemView)Pages.Current).RefreshComparison();
        });
        public Task<bool> AskAsync(string text) { _confirmation = text; return Modals.ShowDialogAsync<ConfirmView, bool>(); }
        public Task BackAsync() => RunAsync(async () =>
        {
            if (Modals.Current != null) { await Modals.RequestDismissAsync(); return; }
            if (Windows.Children.Count > 0)
            { foreach (var window in Windows.Children.Reverse().ToArray()) await Windows.CloseAsync(window); ((ItemView)Pages.Current).RefreshComparison(); return; }
            if (Pages.Current != Home && !(Pages.Current is LoginView)) { await Pages.PopAsync(); return; }
            if (Pages.Current == Home && await AskAsync("返回标题？本地档案已保存。"))
            { await Pages.PopAsync(); Navigation.SetActive(false); }
        });
        public async Task ShutdownAsync()
        {
            await Task.WhenAll(_tasks.ToArray());
            await UI.DisposeAsync();
            Store.Changed -= Refresh;
            UI = null;
        }
    }
}
