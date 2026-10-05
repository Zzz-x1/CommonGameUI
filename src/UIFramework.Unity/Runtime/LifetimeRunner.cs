using CoreNode = global::UIFramework.UINode;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Unity
{
    // Runs before animation frames and survives scene unload. Polling also handles owners
    // which have never been active (Unity does not deliver OnDestroy to those behaviours).
    [DefaultExecutionOrder(-10000)]
    public sealed class LifetimeRunner : MonoBehaviour
    {
        private static LifetimeRunner _instance;
        private readonly List<LifetimeBinding> _bindings = new List<LifetimeBinding>();
        private readonly List<Task> _pending = new List<Task>();
        internal FramePump Frames { get; private set; }
        internal static LifetimeRunner Get()
        {
            if (_instance) return _instance;
            var go = new GameObject("[UIFramework] Lifetime Runner") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<LifetimeRunner>();
            _instance.Frames = go.AddComponent<FramePump>();
            return _instance;
        }
        internal LifetimeBinding Bind(CoreNode node, GameObject owner, bool sceneAnchor)
        {
            if (_bindings.Exists(b => ReferenceEquals(b.Node, node) && !b.Intrinsic)) throw new InvalidOperationException("Node already has a lifetime binding.");
            var binding = new LifetimeBinding(this, node, owner, sceneAnchor); _bindings.Add(binding); return binding;
        }
        internal void BindIntrinsic(CoreNode node, UINode owner)
        {
            if (_bindings.Exists(b => ReferenceEquals(b.Node, node) && b.Intrinsic)) throw new InvalidOperationException("Node already has native lifetime observation.");
            _bindings.Add(new LifetimeBinding(this, node, owner.gameObject, false, owner));
        }
        internal void Remove(LifetimeBinding binding) => _bindings.Remove(binding);
        internal void Observe(Task task) => _pending.Add(task);
        private void Update()
        {
            foreach (var binding in _bindings.ToArray())
                if (binding.IsConnected && (binding.OwnerDestroyed || binding.Node.State == NodeState.Disposing || binding.Node.State == NodeState.Disposed)) binding.End();
            foreach (var task in _pending.ToArray())
            {
                if (!task.IsCompleted) continue;
                _pending.Remove(task);
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { UI.Report(ex, "Bound Unity lifetime cleanup"); throw; }
            }
        }
    }
}
