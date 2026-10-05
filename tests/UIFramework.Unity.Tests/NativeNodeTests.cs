using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UIFramework;
using UIFramework.Unity;
using EngineUI = UIFramework.Unity;
using UnityObject = UnityEngine.Object;

namespace UIFramework.Tests.Unity
{
    public sealed class NativeNodeTests
    {
        private GameObject _canvas, _events;
        private UIContext _ui;
        private readonly List<Exception> _errors = new List<Exception>();
        private sealed class TestLog : IUILog
        {
            private readonly List<Exception> _errors;
            public TestLog(List<Exception> errors) { _errors = errors; }
            public void Error(Exception error, string stage) => _errors.Add(error);
        }
        private PanelContainerNode _windows;
        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            _errors.Clear();
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _events = new GameObject("EventSystem", typeof(EventSystem));
            _ui = EngineUI.UI.Create(_canvas.GetComponent<Canvas>(), _events.GetComponent<EventSystem>(), logger: new TestLog(_errors));
            var mount = new GameObject("Windows", typeof(RectTransform)); mount.transform.SetParent(_canvas.transform, false);
            var creation = _ui.Root.CreateContainerAsync<PanelContainerNode>("Windows", (RectTransform)mount.transform);
            yield return Await(creation); _windows = creation.Result;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return Await(_ui.DisposeAsync().AsTask());
            UnityObject.Destroy(_canvas); UnityObject.Destroy(_events); yield return null;
            Assert.IsEmpty(_errors);
        }
        [UnityTest] public IEnumerator PrefabScriptAndSerializedControlBelongToTheSameInstance()
        {
            _ui.Register<NativeWindow>("Tests/Window", view => view.Configured = true);
            var opening = _windows.OpenAsync<NativeWindow>(); yield return Await(opening);
            var view = opening.Result;
            Assert.AreSame(view, view.GetComponent<NativeWindow>());
            Assert.IsTrue(view.CloseControl.transform.IsChildOf(view.transform));
            Assert.AreSame(view, _windows.Children[0]); Assert.AreEqual(1, view.Initialized);
            var same = _windows.OpenAsync<NativeWindow>(); yield return Await(same); Assert.AreSame(view, same.Result);
            view.CloseControl.onClick.Invoke();
            while (view.State != NodeState.Disposed) yield return null;
            Assert.AreEqual(1, view.Disposals);
            var next = _windows.OpenAsync<NativeWindow>(); yield return Await(next); Assert.AreNotSame(view, next.Result);
        }
        [UnityTest] public IEnumerator InactivePrefabAwakeRunsBeforeFrameworkInitialization()
        {
            _ui.Register<NativeWindow>("Tests/InactiveWindow", view => view.Configured = true);
            var opening = _windows.OpenAsync<NativeWindow>(); yield return Await(opening);
            Assert.AreEqual(1, opening.Result.AwakeCount); Assert.AreEqual(1, opening.Result.Initialized);
        }
        [UnityTest] public IEnumerator MultipleClonesRemapControlReferencesIndependently()
        {
            _ui.Register<NativeWindow>("Tests/Window", view => view.Configured = true, WindowInstanceMode.Multiple);
            var first = _windows.OpenAsync<NativeWindow>(); yield return Await(first);
            var second = _windows.OpenAsync<NativeWindow>(); yield return Await(second);
            Assert.AreNotSame(first.Result.CloseControl, second.Result.CloseControl);
            Assert.IsTrue(first.Result.CloseControl.transform.IsChildOf(first.Result.transform));
            Assert.IsTrue(second.Result.CloseControl.transform.IsChildOf(second.Result.transform));
        }
        [UnityTest] public IEnumerator SerializedReferenceSurvivesRenameReparentAndSave()
        {
            const string path = "Assets/Resources/Tests/Window.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var reference = root.GetComponent<NativeWindow>().CloseControl;
            var group = new GameObject("NewGroup", typeof(RectTransform)); group.transform.SetParent(root.transform, false);
            reference.transform.SetParent(group.transform, false); reference.name = "RenamedClose";
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            _ui.Register<NativeWindow>("Tests/Window", view => view.Configured = true);
            var opening = _windows.OpenAsync<NativeWindow>(); yield return Await(opening);
            Assert.AreEqual("RenamedClose", opening.Result.CloseControl.name);
            Assert.AreEqual("NewGroup", opening.Result.CloseControl.transform.parent.name);
            opening.Result.CloseControl.onClick.Invoke();
            while (opening.Result.State != NodeState.Disposed) yield return null;
        }
        [UnityTest] public IEnumerator ParametersAndDialogResultsUseNativeOwners()
        {
            _ui.Register<NativeParameterWindow>("Tests/Parameter");
            var opening = _windows.OpenAsync<NativeParameterWindow>("sword"); yield return Await(opening);
            Assert.AreEqual("sword", opening.Result.InitializedWith);
            var create = _ui.Root.CreateContainerAsync<ModalContainerNode>("Dialogs", (RectTransform)_canvas.transform); yield return Await(create);
            _ui.Register<NativeDialog>("Tests/Dialog");
            var answer = create.Result.ShowDialogAsync<NativeDialog, bool>();
            while (create.Result.Current == null) yield return null;
            var dialog = (NativeDialog)create.Result.Current; Assert.AreSame(dialog, _ui.InputOwner);
            dialog.Complete(true); yield return Await(answer); Assert.IsTrue(answer.Result); Assert.AreEqual(NodeState.Disposed, dialog.State);
        }
        [UnityTest] public IEnumerator DestroyedNativeOwnerCancelsTransitionAndCleansOnce()
        {
            NativeWindow view = null;
            _ui.Register<NativeWindow>("Tests/Window", node => { view = node; node.Configured = true; });
            _windows.ChildTransition = EngineUI.Transitions.Fade(1);
            var opening = _windows.OpenAsync<NativeWindow>();
            while (view == null || !view.IsTransitioning) yield return null;
            UnityObject.Destroy(view.gameObject);
            while (!opening.IsCompleted || view.State != NodeState.Disposed) yield return null;
            Assert.Catch<OperationCanceledException>(() => opening.GetAwaiter().GetResult());
            Assert.AreEqual(1, view.Disposals); Assert.IsEmpty(_windows.Children);
            Assert.IsTrue(_errors.TrueForAll(error => error is OperationCanceledException)); _errors.Clear();
        }
        [UnityTest] public IEnumerator NativeLifetimeAndExplicitOwnerBindingDoNotConflict()
        {
            _ui.Register<NativeWindow>("Tests/Window", view => view.Configured = true);
            var opening = _windows.OpenAsync<NativeWindow>(); yield return Await(opening);
            var owner = new GameObject("Scope"); var binding = opening.Result.BindLifetime(owner);
            UnityObject.Destroy(owner); yield return Await(binding.Completion);
            Assert.AreEqual(NodeState.Disposed, opening.Result.State); Assert.AreEqual(1, opening.Result.Disposals);
        }
    }
}
