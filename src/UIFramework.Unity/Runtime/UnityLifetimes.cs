using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIFramework.Unity
{
    /// <summary>Disconnecting a binding does not dispose its node or the supplied owner.</summary>
    public sealed class UnityLifetimeBinding : IDisposable
    {
        private readonly TaskCompletionSource<Task> _completion = new TaskCompletionSource<Task>();
        internal readonly UINode Node;
        internal readonly GameObject Owner;
        private readonly bool _sceneAnchor;
        private readonly UnityLifetimeRunner _runner;
        private bool _connected = true;
        internal bool IsConnected => _connected;
        internal UnityLifetimeBinding(UnityLifetimeRunner runner, UINode node, GameObject owner, bool sceneAnchor)
        { _runner = runner; Node = node; Owner = owner; _sceneAnchor = sceneAnchor; Completion = _completion.Task.Unwrap(); }
        /// <summary>Completes after node/context cleanup, or immediately when explicitly disconnected.</summary>
        public Task Completion { get; }
        internal void End()
        {
            Disconnect();
            Task cleanup;
            try
            {
                if (!Owner)
                {
                    Prepare(Node);
                    if (ReferenceEquals(Node, Node.Context.Root)) ((UnityUIHost)Node.Context.Host).BeginLifetimeDisposal();
                }
                cleanup = ReferenceEquals(Node, Node.Context.Root) ? Node.Context.DisposeAsync().AsTask() : Node.DisposeAsync().AsTask();
            }
            catch (Exception ex) { cleanup = Task.FromException(ex); }
            _completion.SetResult(cleanup);
            _runner.Observe(Completion);
        }
        private static void Prepare(UINode node)
        {
            if (node.Element is UnityUIElement display) display.BeginLifetimeDisposal();
            foreach (var child in node.Children) Prepare(child);
        }
        private void Disconnect()
        {
            _connected = false; _runner.Remove(this);
            if (_sceneAnchor && Owner) UnityEngine.Object.Destroy(Owner);
        }
        public void Dispose()
        {
            Node.Context.Host.VerifyAccess();
            if (!_connected) return;
            Disconnect(); _completion.SetResult(Task.CompletedTask);
        }
    }

    public static class UnityLifetimeExtensions
    {
        public static UnityLifetimeBinding BindLifetime(this UINode node, GameObject owner) => Bind(node, owner, false);
        public static UnityLifetimeBinding BindLifetime(this UIContext ui, GameObject owner) => ui.Root.BindLifetime(owner);
        public static UnityLifetimeBinding BindLifetime(this UIContext ui, Scene scene) => ui.Root.BindLifetime(scene);
        public static UnityLifetimeBinding BindLifetime(this UINode node, Scene scene)
        {
            GameObject owner = null;
            try
            {
                Verify(node);
                if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded scene is required.", nameof(scene));
                owner = new GameObject("[UIFramework] Scene Lifetime") { hideFlags = HideFlags.HideInHierarchy };
                SceneManager.MoveGameObjectToScene(owner, scene);
                return Bind(node, owner, true);
            }
            catch (Exception ex)
            {
                if (owner) UnityEngine.Object.Destroy(owner);
                UnityUI.Report(ex, "Bind Unity scene lifetime"); throw;
            }
        }
        private static void Verify(UINode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!(node.Context.Host is UnityUIHost)) throw new InvalidOperationException("Expected a Unity UI context.");
            node.Context.Host.VerifyAccess();
            if (node.State != NodeState.Ready && node.State != NodeState.Initializing) throw new InvalidOperationException("Bind an attached, live node.");
        }
        private static UnityLifetimeBinding Bind(UINode node, GameObject owner, bool sceneAnchor)
        {
            try
            {
                Verify(node);
                if (!owner) throw new ArgumentNullException(nameof(owner));
                if (!owner.scene.IsValid() || !owner.scene.isLoaded) throw new ArgumentException("The owner must belong to a loaded scene.", nameof(owner));
                return UnityLifetimeRunner.Get().Bind(node, owner, sceneAnchor);
            }
            catch (Exception ex) { UnityUI.Report(ex, "Bind Unity lifetime"); throw; }
        }
    }

}
