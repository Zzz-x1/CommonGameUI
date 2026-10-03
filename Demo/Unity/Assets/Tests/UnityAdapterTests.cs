using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarCourt.UnityDemo.Tests
{
    public sealed class UnityAdapterTests
    {
        private GameObject _canvas, _events;
        private UIContext _ui;
        private RectTransform _mount;
        private RecordingLog _log;
        public sealed class Plain : UINode { }
        public sealed class Other : UINode { }
        public sealed class Third : UINode { }
        private sealed class RecordingLog : IUILog
        {
            public readonly List<Exception> Errors = new List<Exception>();
            public void Error(Exception exception, string context) => Errors.Add(exception);
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _events = new GameObject("TestEvents", typeof(EventSystem), typeof(StandaloneInputModule));
            _log = new RecordingLog();
            _ui = UnityUI.Create(_canvas.GetComponent<Canvas>(), _events.GetComponent<EventSystem>(), logger: _log);
            _ui.Register<Plain>("UI/Graphics"); _ui.Register<Other>("UI/Confirm"); _ui.Register<Third>("UI/Purchase");
            _mount = Rect("Mount", _canvas.transform);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return Await(_ui.DisposeAsync().AsTask());
            UnityEngine.Object.Destroy(_canvas); UnityEngine.Object.Destroy(_events);
            yield return null;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static IEnumerator Await(Task task)
        { while (!task.IsCompleted) yield return null; task.GetAwaiter().GetResult(); }
        [UnityTest] public IEnumerator RootAndBorrowedTabs() => Await(RootAndBorrowedTabsAsync());
        private async Task RootAndBorrowedTabsAsync()
        {
            Assert.That(_ui.Root.Children, Is.Empty);
            var tabs = await _ui.Root.CreateContainerAsync<TabContainerNode>("Tabs", _mount);
            var first = Rect("Equipment", _mount); var second = Rect("Materials", _mount);
            await tabs.BindAsync("装备", first); await tabs.BindAsync("材料", second);
            await tabs.SwitchAsync("材料");
            Assert.That(tabs.Current.GetRect(), Is.SameAs(second)); Assert.That(first.gameObject.activeSelf, Is.False);
            await tabs.HideAsync(); Assert.That(tabs.Current, Is.Null);
            await tabs.SwitchAsync("材料"); Assert.That(tabs.Current.GetType(), Is.EqualTo(typeof(UINode)));
            await tabs.DisposeAsync(); Assert.That(first && second && _mount, Is.True);
        }
        [UnityTest] public IEnumerator PrefabHistoryAndPanelOwnership() => Await(PrefabHistoryAndPanelOwnershipAsync());
        private async Task PrefabHistoryAndPanelOwnershipAsync()
        {
            var stack = await _ui.Root.CreateContainerAsync<StackContainerNode>("Pages", _mount);
            var first = await stack.PushAsync<Plain>(); var second = await stack.PushAsync<Other>();
            Assert.That(first.State, Is.EqualTo(NodeState.Ready)); Assert.That(first.GetRect().gameObject.activeSelf, Is.False);
            await stack.PopToAsync(first); Assert.That(stack.Current, Is.SameAs(first)); Assert.That(second.State, Is.EqualTo(NodeState.Disposed));
            var panelMount = Rect("Windows", _canvas.transform);
            var windows = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", panelMount);
            var window = await windows.OpenAsync<Plain>(); var same = await windows.OpenAsync<Plain>(); Assert.That(same, Is.SameAs(window));
            await windows.HideAsync(window); await windows.ShowAsync(window); Assert.That(window.IsInteractive, Is.True);
            await windows.CloseAsync(window); Assert.That(panelMount.childCount, Is.EqualTo(0)); Assert.That(panelMount, Is.Not.Null);
        }
        [UnityTest] public IEnumerator InputGatingPreservesBusinessAndParentGroups() => Await(InputGatingAsync());
        private async Task InputGatingAsync()
        {
            var page = Rect("Page", _mount); var child = Rect("Button", page);
            var button = child.gameObject.AddComponent<Button>(); button.interactable = false;
            var group = child.gameObject.AddComponent<CanvasGroup>(); group.ignoreParentGroups = true;
            var element = new UnityUIElement((UnityUIHost)_ui.Host, page);
            element.SetInteractive(false); Assert.That(group.ignoreParentGroups, Is.False); Assert.That(button.interactable, Is.False);
            element.SetInteractive(true); Assert.That(group.ignoreParentGroups, Is.True); Assert.That(button.interactable, Is.False);
            await element.DisposeAsync(); Assert.That(page, Is.Not.Null);
        }
        [UnityTest] public IEnumerator NestedModalCloseToRestoresFocusAndBackdrop() => Await(NestedModalAsync());
        private async Task NestedModalAsync()
        {
            var original = Rect("Original", _canvas.transform).gameObject.AddComponent<Button>();
            _events.GetComponent<EventSystem>().SetSelectedGameObject(original.gameObject);
            var modals = await _ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", _mount);
            var dismissTasks = new List<Task>(); modals.ConfigureBackdrop(UnityBackdrops.Dim(dismissTasks.Add), ModalBackdropMode.Shared);
            var first = await modals.OpenAsync<Plain>(); var second = await modals.OpenAsync<Other>(); var third = await modals.OpenAsync<Third>();
            Assert.That(first.IsInteractive, Is.False); Assert.That(third.IsInteractive, Is.True);
            await modals.CloseToAsync(first); Assert.That(first.IsInteractive, Is.True); Assert.That(second.State, Is.EqualTo(NodeState.Disposed));
            Assert.That(_mount.GetComponentsInChildren<RectTransform>(true).Count(r => r.name == "ModalBackdrop"), Is.EqualTo(1));
            await modals.CloseAsync(first); Assert.That(_events.GetComponent<EventSystem>().currentSelectedGameObject, Is.SameAs(original.gameObject));
            Assert.That(_mount.childCount, Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator NativeTransitionCancellationAndRawFailure() => Await(TransitionBoundariesAsync());
        private async Task TransitionBoundariesAsync()
        {
            var tabs = await _ui.Root.CreateContainerAsync<TabContainerNode>("Effects", _mount);
            var rect = Rect("Item", _mount); rect.localScale = new Vector3(.8f, .9f, 1);
            var group = rect.gameObject.AddComponent<CanvasGroup>(); group.alpha = .7f;
            var node = new UINode { Transition = UnityTransitions.Simple(new UnityTransitionFrame(offset: new Vector2(100, 40), scale: .4f, alpha: 0), new UnityTransitionFrame(), .3) };
            await tabs.BindAsync("Item", node, rect);
            var scale = rect.localScale; var position = rect.anchoredPosition;
            using var cancellation = new CancellationTokenSource();
            void Cancel(TransitionState state) { if (state.Stage == TransitionStage.Playing && state.Progress > .2f) cancellation.Cancel(); }
            node.TransitionChanged += Cancel;
            try { await tabs.SwitchAsync("Item", cancellationToken: cancellation.Token); Assert.Fail("Expected cancellation."); }
            catch (OperationCanceledException) { }
            node.TransitionChanged -= Cancel;
            Assert.That(rect.localScale, Is.EqualTo(scale)); Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(group.alpha, Is.EqualTo(.7f)); Assert.That(tabs.Current, Is.Null);
            var original = new IOException("Expected native observer failure.");
            void Fail(TransitionState state) { if (state.Stage == TransitionStage.Playing && state.Progress > .2f) throw original; }
            node.TransitionChanged += Fail;
            try { await tabs.SwitchAsync("Item"); Assert.Fail("Expected failure."); }
            catch (IOException ex) { Assert.That(ex, Is.SameAs(original)); Assert.That(_log.Errors, Does.Contain(original)); }
            node.TransitionChanged -= Fail;
            Assert.That(rect.localScale, Is.EqualTo(scale)); Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(group.alpha, Is.EqualTo(.7f));
            await tabs.SwitchAsync("Item"); Assert.That(node.IsInteractive, Is.True);
        }
        [UnityTest] public IEnumerator MissingPrefabAndThreadAccessFailExplicitly() => Await(ErrorsAsync());
        private async Task ErrorsAsync()
        {
            var loader = new UnityPrefabLoader((UnityUIHost)_ui.Host);
            try { await loader.LoadAsync("UI/Absent", CancellationToken.None); Assert.Fail("Expected missing resource."); }
            catch (FileNotFoundException ex) { Assert.That(ex.FileName, Is.EqualTo("UI/Absent")); }
            var exception = await Task.Run(() => Assert.Throws<InvalidOperationException>(() => _ui.Register<ClipNode>("UI/Lobby")));
            Assert.That(_log.Errors, Does.Contain(exception));
        }
        public sealed class ClipNode : UINode
        {
            protected override Task OnInitializeAsync(CancellationToken token)
            {
                var hero = this.GetComponent<RectTransform>("Hero"); var position = hero.anchoredPosition;
                var groups = this.GetRect().GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g => g, g => g.alpha);
                Transition = UnityTransitions.Animation(null, "LobbyEnter", "LobbyExit", _ =>
                { hero.anchoredPosition = position; foreach (var pair in groups) pair.Key.alpha = pair.Value; });
                return Task.CompletedTask;
            }
        }
        [UnityTest] public IEnumerator AnimationClipCompletionAndCancellation() => Await(ClipsAsync());
        private async Task ClipsAsync()
        {
            _ui.Register<ClipNode>("UI/Lobby");
            var windows = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Animation", _mount);
            var firstOpening = windows.OpenAsync<ClipNode>();
            while (windows.Children.Count == 0 || windows.Children[0].TransitionState?.Stage != TransitionStage.Playing || windows.Children[0].TransitionState.Progress < .3)
                await ((UnityUIHost)_ui.Host).NextFrameAsync();
            var moving = windows.Children[0];
            Assert.That(moving.GetComponent<RectTransform>("Hero").anchoredPosition.x, Is.InRange(506, 759));
            Assert.That(moving.GetComponent<CanvasGroup>("Entries").alpha, Is.EqualTo(0));
            var node = await firstOpening;
            Assert.That(node.GetComponent<RectTransform>("Hero").anchoredPosition.x, Is.EqualTo(505));
            Assert.That(node.GetComponent<CanvasGroup>("Hero").alpha, Is.EqualTo(1)); Assert.That(node.IsInteractive, Is.True);
            await windows.CloseAsync(node);
            using var cancellation = new CancellationTokenSource();
            var opening = windows.OpenAsync<ClipNode>(cancellationToken: cancellation.Token);
            while (windows.Children.Count == 0 || windows.Children[0].TransitionState?.Stage != TransitionStage.Playing) await ((UnityUIHost)_ui.Host).NextFrameAsync();
            cancellation.Cancel();
            try { await opening; Assert.Fail("Expected cancellation."); } catch (OperationCanceledException) { }
            Assert.That(windows.Children, Is.Empty); Assert.That(_mount.childCount, Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator AnimationRunsWhileGameplayIsPaused() => Await(PausedClipAsync());
        private async Task PausedClipAsync()
        {
            _ui.Register<ClipNode>("UI/Lobby");
            var windows = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Paused", _mount);
            var original = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                var node = await windows.OpenAsync<ClipNode>();
                Assert.That(node.IsInteractive, Is.True);
                var state = node.GetRect().GetComponent<Animation>()["LobbyEnter"];
                Assert.That(state.speed, Is.EqualTo(1)); Assert.That(state.time, Is.EqualTo(0));
                await windows.CloseAsync(node);
            }
            finally { Time.timeScale = original; }
        }
    }
}
