using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Unity
{
    public sealed class UnityUIElement : IUIElement
    {
        private sealed class GroupDefaults
        {
            public bool Interactive, Raycasts, IgnoreParents;
            public GroupDefaults(CanvasGroup group)
            { Interactive = group.interactable; Raycasts = group.blocksRaycasts; IgnoreParents = group.ignoreParentGroups; }
        }
        private static readonly ConditionalWeakTable<CanvasGroup, GroupDefaults> Defaults = new ConditionalWeakTable<CanvasGroup, GroupDefaults>();
        private readonly UnityUIHost _host;
        private readonly bool _owned;
        private readonly CanvasGroup _group;
        private readonly GroupDefaults _default;
        private readonly (CanvasGroup Group, GroupDefaults Default)[] _groups;
        private bool _disposed;
        public UnityUIElement(UnityUIHost host, RectTransform rect, bool owned = false)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            host.VerifyAccess();
            Rect = rect ? rect : throw new ArgumentNullException(nameof(rect));
            _owned = owned;
            _group = rect.GetComponent<CanvasGroup>();
            if (!_group) _group = rect.gameObject.AddComponent<CanvasGroup>();
            _default = Defaults.GetValue(_group, g => new GroupDefaults(g));
            _groups = rect.GetComponentsInChildren<CanvasGroup>(true)
                .Select(g => (g, Defaults.GetValue(g, item => new GroupDefaults(item)))).ToArray();
        }
        public RectTransform Rect { get; }
        public UnityUIHost Host => _host;
        private void VerifyAlive()
        {
            _host.VerifyAccess();
            if (_disposed || !Rect) throw new ObjectDisposedException(nameof(UnityUIElement));
        }
        public void SetVisible(bool visible) { VerifyAlive(); Rect.gameObject.SetActive(visible); }
        public void SetInteractive(bool interactive)
        {
            VerifyAlive();
            foreach (var pair in _groups)
            {
                if (!pair.Group) throw new ObjectDisposedException("An embedded CanvasGroup was destroyed outside its UI owner.");
                pair.Group.ignoreParentGroups = interactive && pair.Default.IgnoreParents;
            }
            _group.interactable = interactive && _default.Interactive;
            _group.blocksRaycasts = interactive && _default.Raycasts;
            // CanvasGroup gates input without overwriting a business Button.interactable value.
        }
        public void BringToFront() { VerifyAlive(); Rect.SetAsLastSibling(); }
        public object CaptureOrder() { VerifyAlive(); return Rect.GetSiblingIndex(); }
        public void RestoreOrder(object order)
        {
            VerifyAlive();
            if (!(order is int index)) throw new ArgumentException("Expected a sibling index.", nameof(order));
            if (index < 0 || index >= Rect.parent.childCount) throw new ArgumentOutOfRangeException(nameof(order));
            Rect.SetSiblingIndex(index);
        }
        public ValueTask DisposeAsync()
        {
            if (_disposed) return default;
            VerifyAlive();
            if (_owned)
            {
                Rect.gameObject.SetActive(false);
                Rect.SetParent(null, false);
                UnityEngine.Object.Destroy(Rect.gameObject);
            }
            _disposed = true;
            return default;
        }
    }
}
