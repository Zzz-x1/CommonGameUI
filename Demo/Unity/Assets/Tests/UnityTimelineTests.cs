using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
namespace StarCourt.UnityDemo.Tests
{
    public sealed partial class UnityAdapterTests
    {
        private async Task<(TabContainerNode Tabs, UINode Node, RectTransform Rect, PlayableDirector Director)> TimelineTab()
        {
            var tabs = await _ui.Root.CreateContainerAsync<TabContainerNode>("Timeline", _mount);
            var instance = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("UI/Lobby"), _mount);
            var root = (RectTransform)instance.transform;
            var rects = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(r => r, r => (r.anchoredPosition, r.localScale, r.localRotation));
            var groups = root.GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g => g, g => g.alpha);
            var node = new UINode { Transition = UnityTransitions.Timeline("Transitions/Enter", "Transitions/Exit", _ =>
            {
                foreach (var pair in rects) { pair.Key.anchoredPosition = pair.Value.anchoredPosition; pair.Key.localScale = pair.Value.localScale; pair.Key.localRotation = pair.Value.localRotation; }
                foreach (var pair in groups) pair.Key.alpha = pair.Value;
            }) };
            var director = root.Find("Transitions/Enter").GetComponent<PlayableDirector>(); director.time = .13;
            await tabs.BindAsync("Lobby", node, root);
            return (tabs, node, root, director);
        }
        [UnityTest] public IEnumerator TimelineCompletesWhilePausedAndRestoresState() => Await(TimelineCompletion());
        private async Task TimelineCompletion()
        {
            var view = await TimelineTab(); var scale = Time.timeScale; Time.timeScale = 0;
            try
            {
                var entering = view.Tabs.SwitchAsync("Lobby");
                Assert.That(view.Rect.Find("Hero").GetComponent<CanvasGroup>().alpha, Is.EqualTo(0).Within(.01), "First frame must be sampled before returning to the engine.");
                Assert.That(view.Director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
                while (view.Node.TransitionProgress < .3) await ((UnityUIHost)_ui.Host).NextFrameAsync();
                Assert.That(((RectTransform)view.Rect.Find("Hero")).anchoredPosition.x, Is.InRange(506, 759));
                Assert.That(view.Rect.Find("Entries").GetComponent<CanvasGroup>().alpha, Is.EqualTo(0).Within(.01));
                await entering; Assert.That(view.Node.IsInteractive, Is.True); Assert.That(view.Node.TransitionProgress, Is.EqualTo(1));
                Assert.That(view.Director.playableGraph.IsValid(), Is.False); Assert.That(view.Director.time, Is.EqualTo(.13).Within(.001));
                Assert.That(view.Director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.GameTime));
                Assert.That(view.Rect.Find("Hero").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                await view.Tabs.HideAsync(); Assert.That(view.Rect.gameObject.activeSelf, Is.False);
                Assert.That(view.Rect.Find("Transitions/Exit").GetComponent<PlayableDirector>().playableGraph.IsValid(), Is.False);
                await view.Tabs.SwitchAsync("Lobby", false); Assert.That(view.Director.playableGraph.IsValid(), Is.False);
            }
            finally { Time.timeScale = scale; }
        }
        [UnityTest] public IEnumerator TimelineCancellationRestoresAndCanReplay() => Await(TimelineCancel());
        private async Task TimelineCancel()
        {
            var view = await TimelineTab(); using var cancel = new CancellationTokenSource();
            void Cancelling(TransitionState state) { if (state.Stage == TransitionStage.Playing && state.Progress > .1) cancel.Cancel(); }
            view.Node.TransitionChanged += Cancelling;
            try { await view.Tabs.SwitchAsync("Lobby", cancellationToken: cancel.Token); Assert.Fail("Expected cancellation."); } catch (OperationCanceledException) { }
            view.Node.TransitionChanged -= Cancelling;
            Assert.That(view.Tabs.Current, Is.Null); Assert.That(view.Director.playableGraph.IsValid(), Is.False);
            Assert.That(view.Rect.Find("Hero").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            await view.Tabs.SwitchAsync("Lobby"); Assert.That(view.Node.IsInteractive, Is.True);
        }
        [UnityTest] public IEnumerator TimelineExternalStopAndObserverFailureRemainErrors() => Await(TimelineFailures());
        private async Task TimelineFailures()
        {
            var view = await TimelineTab();
            var playing = view.Tabs.SwitchAsync("Lobby"); view.Director.Stop();
            try { await playing; Assert.Fail("Expected interrupted Timeline failure."); }
            catch (InvalidOperationException error) { Assert.That(error.Message, Does.Contain("interrupted")); Assert.That(_log.Errors, Does.Contain(error)); }
            var original = new System.IO.IOException("Timeline observer identity");
            void Failure(TransitionState state) { if (state.Stage == TransitionStage.Playing && state.Progress > .1) throw original; }
            view.Node.TransitionChanged += Failure;
            try { await view.Tabs.SwitchAsync("Lobby"); Assert.Fail("Expected observer failure."); }
            catch (System.IO.IOException error) { Assert.That(error, Is.SameAs(original)); }
            view.Node.TransitionChanged -= Failure;
            Assert.That(view.Director.playableGraph.IsValid(), Is.False); await view.Tabs.SwitchAsync("Lobby"); Assert.That(view.Node.IsInteractive, Is.True);
        }
        [UnityTest] public IEnumerator TimelineRejectsInvalidDirectorConfiguration() => Await(TimelineInvalid());
        private async Task TimelineInvalid()
        {
            var view = await TimelineTab(); view.Director.playOnAwake = true;
            try { await view.Tabs.SwitchAsync("Lobby"); Assert.Fail("Expected invalid Play On Awake."); } catch (InvalidOperationException error) { Assert.That(error.Message, Does.Contain("Play On Awake")); }
            view.Director.playOnAwake = false; var asset = view.Director.playableAsset; view.Director.playableAsset = null;
            try { await view.Tabs.SwitchAsync("Lobby"); Assert.Fail("Expected missing asset."); } catch (InvalidOperationException error) { Assert.That(error.Message, Does.Contain("asset")); }
            view.Director.playableAsset = asset; await view.Tabs.SwitchAsync("Lobby"); Assert.That(view.Node.IsInteractive, Is.True);
        }
        [UnityTest] public IEnumerator TimelineBoundDestructionCancelsWithoutWritingDeadControls() => Await(TimelineDestruction());
        private async Task TimelineDestruction()
        {
            var view = await TimelineTab(); var lifetime = view.Node.BindLifetime(view.Rect.gameObject);
            var entering = view.Tabs.SwitchAsync("Lobby"); UnityEngine.Object.Destroy(view.Rect.gameObject);
            try { await entering; Assert.Fail("Expected lifetime cancellation."); } catch (OperationCanceledException) { }
            await lifetime.Completion; Assert.That(view.Node.State, Is.EqualTo(NodeState.Disposed)); Assert.That(view.Tabs.Children, Is.Empty);
            Assert.That((bool)view.Director, Is.False);
        }
    }
}
