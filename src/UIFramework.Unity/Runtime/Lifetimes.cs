using CoreNode = global::UIFramework.UINode;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIFramework.Unity
{
    /// <summary>Disconnecting a binding does not dispose its node or the supplied owner.</summary>
    public sealed class LifetimeBinding : IDisposable
    {
        private readonly TaskCompletionSource<Task> _completion = new TaskCompletionSource<Task>();
        internal readonly CoreNode Node;
        internal readonly GameObject Owner;
        internal readonly Component NativeOwner;
        internal bool Intrinsic => !ReferenceEquals(NativeOwner, null);
        internal bool OwnerDestroyed => !Owner || (Intrinsic && !NativeOwner);
        private readonly bool _sceneAnchor;
        private readonly LifetimeRunner _runner;
        private bool _connected = true;
        internal bool IsConnected => _connected;
        internal LifetimeBinding(LifetimeRunner runner, CoreNode node, GameObject owner, bool sceneAnchor, Component nativeOwner = null)
        { NativeOwner = nativeOwner; _runner = runner; Node = node; Owner = owner; _sceneAnchor = sceneAnchor; Completion = _completion.Task.Unwrap(); }
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
                    if (ReferenceEquals(Node, Node.Context.Root)) ((UIHost)Node.Context.Host).BeginLifetimeDisposal();
                }
                cleanup = ReferenceEquals(Node, Node.Context.Root) ? Node.Context.DisposeAsync().AsTask() : Node.DisposeAsync().AsTask();
            }
            catch (Exception ex) { cleanup = Task.FromException(ex); }
            _completion.SetResult(cleanup);
            _runner.Observe(Completion);
        }
        private static void Prepare(CoreNode node)
        {
            if (node.Element is UIElement display) display.BeginLifetimeDisposal();
            foreach (var child in node.Children) Prepare(child.Node);
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

    public static class LifetimeExtensions
    {
        public static LifetimeBinding BindLifetime(this IUINode node, GameObject owner) => Bind(node.Node, owner, false);
        public static LifetimeBinding BindLifetime(this UIContext ui, GameObject owner) => ui.Root.BindLifetime(owner);
        public static LifetimeBinding BindLifetime(this UIContext ui, Scene scene) => ui.Root.BindLifetime(scene);
        public static LifetimeBinding BindLifetime(this IUINode node, Scene scene)
        {
            GameObject owner = null;
            try
            {
                Verify(node.Node);
                if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded scene is required.", nameof(scene));
                owner = new GameObject("[UIFramework] Scene Lifetime") { hideFlags = HideFlags.HideInHierarchy };
                SceneManager.MoveGameObjectToScene(owner, scene);
                return Bind(node.Node, owner, true);
            }
            catch (Exception ex)
            {
                if (owner) UnityEngine.Object.Destroy(owner);
                UI.Report(ex, "Bind Unity scene lifetime"); throw;
            }
        }
        private static void Verify(CoreNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!(node.Context.Host is UIHost)) throw new InvalidOperationException("Expected a Unity UI context.");
            node.Context.Host.VerifyAccess();
            if (node.State != NodeState.Ready && node.State != NodeState.Initializing) throw new InvalidOperationException("Bind an attached, live node.");
        }
        private static LifetimeBinding Bind(CoreNode node, GameObject owner, bool sceneAnchor)
        {
            try
            {
                Verify(node);
                if (!owner) throw new ArgumentNullException(nameof(owner));
                if (!owner.scene.IsValid() || !owner.scene.isLoaded) throw new ArgumentException("The owner must belong to a loaded scene.", nameof(owner));
                return LifetimeRunner.Get().Bind(node, owner, sceneAnchor);
            }
            catch (Exception ex) { UI.Report(ex, "Bind Unity lifetime"); throw; }
        }
    }

}
