using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Unity
{
    // Runs before animation frames and survives scene unload. Polling also handles owners
    // which have never been active (Unity does not deliver OnDestroy to those behaviours).
    [DefaultExecutionOrder(-10000)]
    public sealed class UnityLifetimeRunner : MonoBehaviour
    {
        private static UnityLifetimeRunner _instance;
        private readonly List<UnityLifetimeBinding> _bindings = new List<UnityLifetimeBinding>();
        private readonly List<Task> _pending = new List<Task>();
        internal UnityFramePump Frames { get; private set; }
        internal static UnityLifetimeRunner Get()
        {
            if (_instance) return _instance;
            var go = new GameObject("[UIFramework] Lifetime Runner") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<UnityLifetimeRunner>();
            _instance.Frames = go.AddComponent<UnityFramePump>();
            return _instance;
        }
        internal UnityLifetimeBinding Bind(UINode node, GameObject owner, bool sceneAnchor)
        {
            if (_bindings.Exists(b => ReferenceEquals(b.Node, node))) throw new InvalidOperationException("Node already has a lifetime binding.");
            var binding = new UnityLifetimeBinding(this, node, owner, sceneAnchor); _bindings.Add(binding); return binding;
        }
        internal void Remove(UnityLifetimeBinding binding) => _bindings.Remove(binding);
        internal void Observe(Task task) => _pending.Add(task);
        private void Update()
        {
            foreach (var binding in _bindings.ToArray())
                if (binding.IsConnected && (!binding.Owner || binding.Node.State == NodeState.Disposing || binding.Node.State == NodeState.Disposed)) binding.End();
            foreach (var task in _pending.ToArray())
            {
                if (!task.IsCompleted) continue;
                _pending.Remove(task);
                try { task.GetAwaiter().GetResult(); }
                catch (Exception ex) { UnityUI.Report(ex, "Bound Unity lifetime cleanup"); throw; }
            }
        }
    }
}
