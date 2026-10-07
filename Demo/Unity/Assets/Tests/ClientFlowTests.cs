using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarCourt.UnityDemo.Tests
{
    public sealed class ClientFlowTests
    {
        private GameObject _root;
        private UnityClient _app;
        private string _save;
        private static IEnumerator Await(Task task)
        { while (!task.IsCompleted) yield return null; task.GetAwaiter().GetResult(); }
        [UnitySetUp] public IEnumerator SetUp()
        {
            // The client's supported validation sizes use a 16:9 Game View.
            Type.GetType("StarCourt.UnityDemo.Editor.ClientSceneAuthoring, StarCourt.UnityDemo.Editor", true)
                .GetMethod("SetResolution").Invoke(null, new object[] { 1600, 900 });
            yield return null;
            _save = "client-test-" + Guid.NewGuid().ToString("N") + ".json";
            _root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("UI/ClientRoot"));
            _app = _root.GetComponentInChildren<UnityClient>(); _app.SaveFile = _save;
            yield return null;
            yield return Await(_app.Ready);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return Await(_app.ShutdownAsync());
            UnityEngine.Object.Destroy(_root); yield return null;
            File.Delete(Path.Combine(Application.persistentDataPath, _save));
        }
        private async Task Frame() => await ((EngineUI.UIHost)_app.UI.Host).NextFrameAsync();
        private async Task Click(IUINode node, string path) => await Click(node.GetControl<Button>(path));
        private async Task Click(Button button)
        {
            await Frame(); Canvas.ForceUpdateCanvases();
            Assert.That(button.IsInteractable(), Is.True, button.name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(_app.Events) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); _app.Events.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "No GUI hit for " + button.name + " at " + pointer.position + " screen " + Screen.width + "x" + Screen.height + " groups " + string.Join(";", button.GetComponentsInParent<CanvasGroup>().Select(g => g.name + ":" + g.alpha + "/" + g.interactable + "/" + g.blocksRaycasts)));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), "Blocked GUI hit: " + button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private async Task ClickAndWait(IUINode node, string path) { await Click(node, path); await _app.WhenIdleAsync(); }
        private async Task Route(string name)
        {
            await Click(_app.Navigation.transform.Find(name).GetComponent<Button>()); await _app.WhenIdleAsync();
        }
        private async Task WaitFor<T>() where T : class, IUINode
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!(_app.Modals.Current is T) || !_app.Modals.Current.IsInteractive)
            { Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Waiting for " + typeof(T).Name); await Frame(); }
        }
        private sealed class FrameBackdropFactory : IUIBackdropFactory
        {
            private readonly IUIBackdropFactory _inner;
            public ConfirmView Window;
            public int PlayingFrame;
            public int Shows, Hides;
            public FrameBackdropFactory(IUIBackdropFactory inner) => _inner = inner;
            public async Task<IUIBackdrop> CreateAsync(IUIElement mount, Func<Task> dismiss, System.Threading.CancellationToken token)
            {
                var backdrop = await _inner.CreateAsync(mount, dismiss, token);
                var group = ((EngineUI.UIElement)mount).Rect.Find("ModalBackdrop").GetComponent<CanvasGroup>();
                return new FrameBackdrop(this, backdrop, group);
            }
            private sealed class FrameBackdrop : IUIBackdrop
            {
                private readonly FrameBackdropFactory _owner;
                private readonly IUIBackdrop _inner;
                private readonly CanvasGroup _group;
                public FrameBackdrop(FrameBackdropFactory owner, IUIBackdrop inner, CanvasGroup group)
                { _owner = owner; _inner = inner; _group = group; }
                private Task Play(bool show, bool animate, System.Threading.CancellationToken token)
                {
                    Assert.That(Time.frameCount, Is.EqualTo(_owner.PlayingFrame));
                    Assert.That(_owner.Window.gameObject.activeInHierarchy, Is.True);
                    Assert.That(_owner.Window.IsInteractive, Is.False);
                    var windowAlpha = _owner.Window.GetComponent<CanvasGroup>().alpha;
                    Assert.That(windowAlpha, Is.EqualTo(show ? 0 : 1).Within(.01), "Window initial pose must precede the first frame wait");
                    var task = show ? _inner.ShowAsync(animate, token) : _inner.HideAsync(animate, token);
                    Assert.That(_group.gameObject.activeInHierarchy, Is.True);
                    Assert.That(_group.alpha, Is.EqualTo(show ? 0 : 1).Within(.01));
                    Assert.That(Time.frameCount, Is.EqualTo(_owner.PlayingFrame));
                    if (show) _owner.Shows++; else _owner.Hides++;
                    return task;
                }
                public Task ShowAsync(bool animate, System.Threading.CancellationToken token) => Play(true, animate, token);
                public Task HideAsync(bool animate, System.Threading.CancellationToken token) => Play(false, animate, token);
                public void Restore(bool visible) => _inner.Restore(visible);
                public void SetVisible(bool visible) => _inner.SetVisible(visible);
                public void SetInteractive(bool interactive) => _inner.SetInteractive(interactive);
                public void BringToFront() => _inner.BringToFront();
                public object CaptureOrder() => _inner.CaptureOrder();
                public void RestoreOrder(object order) => _inner.RestoreOrder(order);
                public ValueTask DisposeAsync() => _inner.DisposeAsync();
            }
        }
        [UnityTest] public IEnumerator ModalWindowAndBackdropStartInTheSameFrame() => Await(ModalFirstFrame());
        private async Task ModalFirstFrame()
        {
            await using var ui = new UIContext(_app.UI.Host, _app.UI.Loader);
            var modals = await ui.Root.CreateContainerAsync<ModalContainerNode>("FrameCheck", _app.Modals.GetRoot());
            var factory = new FrameBackdropFactory(EngineUI.Backdrops.Dim(_app.Track, duration: .12));
            modals.ConfigureBackdrop(factory);
            ui.Register<ConfirmView>("UI/Confirm", window =>
            {
                factory.Window = window;
                window.Transition = EngineUI.Transitions.ScaleFade(.12);
                window.TransitionChanged += state => { if (state.Stage == TransitionStage.Playing) factory.PlayingFrame = Time.frameCount; };
            });
            var window = await modals.OpenAsync<ConfirmView>("首帧同步检查");
            Assert.That(factory.Shows, Is.EqualTo(1)); Assert.That(window.IsInteractive, Is.True);
            await modals.CloseAsync(window); Assert.That(factory.Hides, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator PurchaseEquipmentTrainingQuestAndRestart() => Await(PurchaseFlow());
        private async Task PurchaseFlow()
        {
            await ClickAndWait(_app.Pages.Current, "Panel/Enter"); var home = _app.Home;
            Assert.That(_app.Pages.Current, Is.SameAs(home));
            await Route("Shop"); await ClickAndWait(_app.Pages.Current, "Cards/C0");
            var purchase = _app.Modals.Current; int gold = _app.Store.State.Gold;
            await Click(purchase, "Panel/Buy"); await WaitFor<ConfirmView>();
            await ClickAndWait(_app.Modals.Current, "Panel/No"); Assert.That(_app.Store.State.Gold, Is.EqualTo(gold));
            await Click(purchase, "Panel/Buy"); await WaitFor<ConfirmView>();
            await ClickAndWait(_app.Modals.Current, "Panel/Yes"); Assert.That(_app.Modals.Current, Is.TypeOf<RewardView>());
            await ClickAndWait(_app.Modals.Current, "Panel/Close"); Assert.That(_app.Modals.Current, Is.SameAs(purchase));
            await ClickAndWait(purchase, "Panel/Close"); await Route("Characters");
            await ClickAndWait(_app.Pages.Current, "Cards/C0"); var hero = _app.Pages.Current;
            await Click(hero, "Panel/Equip"); await WaitFor<ConfirmView>(); await ClickAndWait(_app.Modals.Current, "Panel/Yes");
            await ClickAndWait(hero, "Panel/Train"); await ClickAndWait(_app.Modals.Current, "Panel/Close");
            Assert.That(_app.Store.State.Equipment[0], Is.EqualTo(0)); Assert.That(_app.Store.State.Levels[0], Is.EqualTo(13));
            Assert.That(hero.GetControl<Text>("Panel/Text").text, Does.StartWith("攻击 340"));
            await Route("Quests"); await ClickAndWait(_app.Pages.Current, "Panel/Claim"); await ClickAndWait(_app.Modals.Current, "Panel/Close");
            Assert.That(_app.Pages.Current.GetControl<Button>("Panel/Claim").interactable, Is.False);
            var saved = new ClientStore(Path.Combine(Application.persistentDataPath, _save)).State;
            Assert.That(saved.Gold, Is.EqualTo(_app.Store.State.Gold)); Assert.That(saved.Inventory[0], Is.EqualTo(3)); Assert.That(saved.QuestClaimed, Is.True);
            await Route("Home"); Assert.That(_app.Pages.Current, Is.SameAs(home));
        }
        [UnityTest] public IEnumerator TabsComparisonSettingsAndTeamRestrictions() => Await(ContainersFlow());
        private async Task ContainersFlow()
        {
            await ClickAndWait(_app.Pages.Current, "Panel/Enter"); await Route("Inventory");
            var inventory = (InventoryView)_app.Pages.Current;
            await ClickAndWait(inventory, "Tabs/材料"); Assert.That(inventory.Tabs.CurrentName, Is.EqualTo("材料"));
            await ClickAndWait(inventory, "Tabs/装备"); await ClickAndWait(inventory, "Pages/装备/InventoryRow0"); var item = _app.Pages.Current;
            await ClickAndWait(item, "Panel/Compare"); await ClickAndWait(item, "Panel/Compare");
            Assert.That(_app.Windows.Children.Count, Is.EqualTo(2)); Assert.That(item.GetControl<Button>("Panel/Compare").interactable, Is.False);
            await ClickAndWait(_app.Windows.Children[1], "Panel/Close"); Assert.That(item.GetControl<Button>("Panel/Compare").interactable, Is.True);
            await Route("Characters"); Assert.That(_app.Windows.Children, Is.Empty);
            await _app.BackAsync(); Assert.That(_app.Pages.Current, Is.SameAs(item));
            Assert.That(item.GetControl<Button>("Panel/Compare").interactable, Is.True);
            await ClickAndWait(item, "Panel/Compare"); await _app.BackAsync(); Assert.That(_app.Windows.Children, Is.Empty);
            await _app.BackAsync(); Assert.That(_app.Pages.Current, Is.SameAs(inventory));
            await _app.BackAsync(); Assert.That(inventory.IsCached, Is.True);
            await Route("Inventory"); Assert.That(_app.Pages.Current, Is.SameAs(inventory));
            Assert.That(inventory.Tabs.CurrentName, Is.EqualTo("装备"));
            await _app.OpenItemAsync(1); Assert.That(_app.Pages.Current, Is.SameAs(item));
            Assert.That(item.GetControl<Text>("Panel/Title").text, Is.EqualTo(ClientStore.Items[1]));
            await _app.BackAsync();
            await Route("Settings"); var settings = (SettingsView)_app.Modals.Current;
            await ClickAndWait(settings, "Panel/Pages/画面/Graphics"); var graphics = _app.Modals.Current;
            graphics.GetControl<Dropdown>("Panel/Quality").value = 0;
            await ClickAndWait(graphics, "Panel/Close"); Assert.That(_app.Modals.Current, Is.SameAs(settings));
            await ClickAndWait(settings, "Panel/Tabs/演出"); settings.GetControl<Toggle>("Panel/Pages/演出/Motion").isOn = false;
            Assert.That(_app.Store.State.FullMotion, Is.False);
            await Click(settings, "Panel/Defaults"); await WaitFor<ConfirmView>(); await ClickAndWait(_app.Modals.Current, "Panel/Yes");
            Assert.That(_app.Store.State.FullMotion, Is.True); Assert.That(_app.Store.State.Quality, Is.EqualTo(2));
            await ClickAndWait(settings, "Panel/Close"); await Route("Team"); var team = _app.Pages.Current;
            team.GetControl<Dropdown>("Slots/S2/Hero").value = 1;
            Assert.That(team.GetControl<Button>("Slots/S2/Assign").interactable, Is.False);
            team.GetControl<Dropdown>("Slots/S2/Hero").value = 3;
            await ClickAndWait(team, "Slots/S2/Assign"); Assert.That(_app.Store.State.Team[2], Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator ActualSourceAndSettingsSelectDifferentAnimations() => Await(AnimationBranches());
        private async Task AnimationBranches()
        {
            await _app.EnterAsync(); await _app.OpenAsync("Shop");
            var entering = _app.OpenAsync("Inventory");
            while (!_app.Pages.Children.OfType<InventoryView>().Any(n => n.TransitionState?.Stage == TransitionStage.Playing && n.TransitionState.Progress > .05)) await Frame();
            Assert.That(_app.Pages.Children.OfType<InventoryView>().Last().GetRoot().anchoredPosition.x, Is.LessThan(0));
            await entering;
            entering = _app.OpenAsync("Shop");
            while (!_app.Pages.Children.OfType<ShopView>().Any(n => n.TransitionState?.Stage == TransitionStage.Playing && n.TransitionState.Progress > .05)) await Frame();
            Assert.That(_app.Pages.Children.OfType<ShopView>().Last().GetRoot().anchoredPosition.x, Is.GreaterThan(0));
            await entering; await _app.OpenPurchaseAsync(0);
            var reward = _app.RewardAsync("购买奖励"); await WaitForAnimation();
            Assert.That(_app.Modals.Children.OfType<RewardView>().Last().GetControl<PlayableDirector>("Transitions/Enter").state == PlayState.Playing, Is.True);
            await reward; await _app.BackAsync(); await _app.BackAsync(); await _app.OpenAsync("Quests");
            reward = _app.RewardAsync("任务奖励"); await WaitForAnimation();
            Assert.That(_app.Modals.Children.OfType<RewardView>().Last().GetControl<PlayableDirector>("Transitions/Enter").playableGraph.IsValid(), Is.False);
            await reward; await _app.BackAsync(); await _app.OpenAsync("Shop"); await _app.OpenPurchaseAsync(0);
            _app.Store.Change(s => s.FullMotion = false);
            reward = _app.RewardAsync("简化购买奖励"); await WaitForAnimation();
            Assert.That(_app.Modals.Children.OfType<RewardView>().Last().GetControl<PlayableDirector>("Transitions/Enter").playableGraph.IsValid(), Is.False);
            await reward;
        }
        private async Task WaitForAnimation()
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!_app.Modals.Children.OfType<RewardView>().Any(n => n.TransitionState?.Stage == TransitionStage.Playing && n.TransitionState.Progress > .05))
            { Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Reward animation was not sampled."); await Frame(); }
        }
    }
}

