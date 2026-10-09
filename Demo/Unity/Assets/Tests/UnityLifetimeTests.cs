using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarCourt.UnityDemo.Tests
{
    public sealed class UnityLifetimeTests
    {
        private GameObject _canvas, _events;
        private UIContext _ui;
        private Loader _loader;
        private RecordingLog _log;
        private sealed class RecordingLog : IUILog
        {
            public readonly List<Exception> Errors = new List<Exception>();
            public void Error(string message) => new ConsoleUILog().Error(message);
            public void Exception(Exception exception, string context) => Errors.Add(exception);
        }
        public sealed class Window : UINode { }
        private sealed class DisconnectingNode : UINode
        {
            internal EngineUI.LifetimeBinding Binding;
            protected override Task OnDisposeAsync() { Binding.Dispose(); return Task.CompletedTask; }
        }
        private sealed class GatedNode : UINode
        {
            internal readonly TaskCompletionSource<bool> Gate = new TaskCompletionSource<bool>();
            protected override Task OnDisposeAsync() => Gate.Task;
        }
        private sealed class FailingNode : UINode
        {
            internal readonly Exception Failure = new InvalidOperationException("lifetime cleanup failure");
            protected override Task OnDisposeAsync() => Task.FromException(Failure);
        }
        private sealed class Loader : IUIResourceLoader
        {
            internal int Released;
            public Task<IUIResource> LoadAsync(string path, CancellationToken token) => Task.FromResult<IUIResource>(new Resource(this));
            private sealed class Resource : IUIResource
            {
                private readonly Loader _loader;
                internal Resource(Loader loader) { _loader = loader; }
                public IUIElement Instantiate(IUIElement mount)
                {
                    var target = (EngineUI.UIElement)mount;
                    var rect = (RectTransform)new GameObject("Window", typeof(RectTransform)).transform;
                    rect.SetParent(target.Rect, false);
                    return new EngineUI.UIElement(target.Host, rect, true);
                }
                public void Dispose() => _loader.Released++;
            }
        }
        private static IEnumerator Await(Task task) { while (!task.IsCompleted) yield return null; task.GetAwaiter().GetResult(); }
        [UnitySetUp] public IEnumerator SetUp()
        {
            _canvas = new GameObject("LifetimeCanvas", typeof(RectTransform), typeof(Canvas));
            _events = new GameObject("LifetimeEvents", typeof(EventSystem));
            _loader = new Loader(); _log = new RecordingLog();
            _ui = EngineUI.UI.Create(_canvas.GetComponent<Canvas>(), _events.GetComponent<EventSystem>(), _loader, _log);
            _ui.Register(() => new Window(), "window", WindowInstanceMode.Multiple);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return Await(_ui.DisposeAsync().AsTask());
            UnityEngine.Object.Destroy(_canvas); UnityEngine.Object.Destroy(_events); yield return null;
        }
        [UnityTest] public IEnumerator InactiveOwnerClosesSubtreeAndKeepsContext() => Await(InactiveOwnerAsync());
        private async Task InactiveOwnerAsync()
        {
            var owner = new GameObject("NeverActive"); owner.SetActive(false);
            var node = new UINode(); await node.AttachAsync(_ui.Root, "Scope");
            var child = new UINode(); await child.AttachAsync(node, "Child");
            var binding = node.BindLifetime(owner);
            UnityEngine.Object.Destroy(owner); await binding.Completion;
            Assert.That(node.State, Is.EqualTo(NodeState.Disposed)); Assert.That(child.State, Is.EqualTo(NodeState.Disposed));
            Assert.That(_ui.Root.State, Is.EqualTo(NodeState.Ready)); Assert.That(_log.Errors, Is.Empty);
        }
        [UnityTest] public IEnumerator SceneUnloadReleasesContextModalsBackdropsAndResources() => Await(SceneAsync());
        private async Task SceneAsync()
        {
            var scene = SceneManager.CreateScene("Lifetime-" + Guid.NewGuid());
            SceneManager.MoveGameObjectToScene(_canvas, scene); SceneManager.MoveGameObjectToScene(_events, scene);
            var dialogs = await _ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", (RectTransform)_canvas.transform);
            dialogs.ConfigureBackdrop(EngineUI.Backdrops.Dim(_ => Assert.Fail("Unexpected dismiss")), ModalBackdropMode.PerModal);
            var first = await dialogs.OpenAsync<Window>(playAnimation: false); var second = await dialogs.OpenAsync<Window>(playAnimation: false);
            var binding = _ui.BindLifetime(scene);
            Assert.That(Array.Exists(scene.GetRootGameObjects(), go => go.name == "[UIFramework] Scene Lifetime" && (go.hideFlags & HideFlags.HideInHierarchy) != 0), Is.True);
            var unload = SceneManager.UnloadSceneAsync(scene);
            await binding.Completion;
            Assert.That(_ui.Root.State, Is.EqualTo(NodeState.Disposed)); Assert.That(first.State, Is.EqualTo(NodeState.Disposed));
            Assert.That(second.State, Is.EqualTo(NodeState.Disposed)); Assert.That(_loader.Released, Is.EqualTo(1)); Assert.That(_log.Errors, Is.Empty);
            while (!unload.isDone) await ((EngineUI.UIHost)_ui.Host).NextFrameAsync();
        }
        [UnityTest] public IEnumerator DisconnectAndDuplicateBinding() => Await(DisconnectAsync());
        private async Task DisconnectAsync()
        {
            var owner = new GameObject("Disconnect"); var node = new UINode(); await node.AttachAsync(_ui.Root, "Scope");
            var binding = node.BindLifetime(owner);
            LogAssert.Expect(LogType.Error, "[UI] Bind Unity lifetime");
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Node already has a lifetime binding"));
            Assert.Throws<InvalidOperationException>(() => node.BindLifetime(owner));
            binding.Dispose(); UnityEngine.Object.Destroy(owner); await binding.Completion;
            Assert.That(node.State, Is.EqualTo(NodeState.Ready));
            owner = new GameObject("Explicit"); binding = node.BindLifetime(owner);
            await node.DisposeAsync(); await binding.Completion; Assert.That(owner, Is.Not.Null);
            UnityEngine.Object.Destroy(owner);
            owner = new GameObject("Overlapping"); var rootBinding = _ui.BindLifetime(owner);
            var nested = new DisconnectingNode(); await nested.AttachAsync(_ui.Root, "Nested"); nested.Binding = nested.BindLifetime(owner);
            UnityEngine.Object.Destroy(owner); await rootBinding.Completion; await nested.Binding.Completion;
            Assert.That(nested.State, Is.EqualTo(NodeState.Disposed));
        }
        [UnityTest] public IEnumerator AsyncCleanupSurvivesDestroyedOwner() => Await(GatedAsync());
        private async Task GatedAsync()
        {
            var owner = new GameObject("Async"); var node = new GatedNode(); await node.AttachAsync(_ui.Root, "Async");
            var binding = node.BindLifetime(owner); UnityEngine.Object.Destroy(owner);
            while (node.State != NodeState.Disposing) await ((EngineUI.UIHost)_ui.Host).NextFrameAsync();
            Assert.That(binding.Completion.IsCompleted, Is.False);
            node.Gate.SetResult(true); await binding.Completion; Assert.That(node.State, Is.EqualTo(NodeState.Disposed));
        }
        [UnityTest] public IEnumerator FadeAndTimelineCancelWhenOwnerIsDestroyed() => Await(AnimationsAsync());
        private async Task AnimationsAsync()
        {
            foreach (bool timeline in new[] { false, true })
            {
                var rect = (RectTransform)new GameObject("AnimationScope", typeof(RectTransform)).transform; rect.SetParent(_canvas.transform, false);
                var tabs = await _ui.Root.CreateContainerAsync<TabContainerNode>("Tabs" + timeline, rect);
                var page = (RectTransform)new GameObject("Page", typeof(RectTransform), typeof(CanvasGroup)).transform; page.SetParent(rect, false);
                AnimationClip clip = null;
                if (timeline)
                {
                    var player = page.gameObject.AddComponent<Animation>(); clip = new AnimationClip { legacy = true };
                    clip.SetCurve("", typeof(CanvasGroup), "m_Alpha", AnimationCurve.Linear(0, 0, 1, 1)); player.AddClip(clip, "enter"); player.AddClip(clip, "exit");
                }
                var node = new UINode { Transition = timeline ? EngineUI.Transitions.Animation(null, "enter", "exit", _ => Assert.Fail("Restoring a destroyed timeline")) : EngineUI.Transitions.Fade(1) };
                await tabs.BindAsync("Page", node, page); var binding = tabs.BindLifetime(rect.gameObject);
                var pending = tabs.SwitchAsync("Page"); UnityEngine.Object.Destroy(rect.gameObject);
                try { await pending; Assert.Fail("Expected cancellation"); } catch (OperationCanceledException) { }
                await binding.Completion; Assert.That(tabs.State, Is.EqualTo(NodeState.Disposed));
                if (clip) UnityEngine.Object.Destroy(clip);
            }
        }
        [UnityTest] public IEnumerator CleanupFailureIsObservedAndPreservesOriginalException() => Await(FailureAsync());
        private async Task FailureAsync()
        {
            var owner = new GameObject("Failure"); var node = new FailingNode(); await node.AttachAsync(_ui.Root, "Failure");
            var binding = node.BindLifetime(owner);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: lifetime cleanup failure"));
            UnityEngine.Object.Destroy(owner);
            try { await binding.Completion; Assert.Fail("Expected cleanup failure"); }
            catch (InvalidOperationException error) { Assert.That(error, Is.SameAs(node.Failure)); Assert.That(_log.Errors, Does.Contain(error)); }
            await ((EngineUI.UIHost)_ui.Host).NextFrameAsync();
        }
        [UnityTest] public IEnumerator UnboundDestroyedDisplayStillFails() => Await(UnboundAsync());
        private async Task UnboundAsync()
        {
            var rect = (RectTransform)new GameObject("Unbound", typeof(RectTransform)).transform; rect.SetParent(_canvas.transform, false);
            var node = new UINode(); await node.AttachToAsync(_ui.Root, "Unbound", rect);
            UnityEngine.Object.Destroy(rect.gameObject); await ((EngineUI.UIHost)_ui.Host).NextFrameAsync();
            try { await node.DisposeAsync(); Assert.Fail("Expected missing display error"); }
            catch (ObjectDisposedException error) { Assert.That(_log.Errors, Does.Contain(error)); }
        }
    }
}
