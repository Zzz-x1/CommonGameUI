using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Unity
{
    public sealed class UIElement : IUIElement, IUIResourceNodeProvider
    {
        private sealed class GroupDefaults
        {
            public bool Interactive, Raycasts, IgnoreParents;
            public GroupDefaults(CanvasGroup group)
            { Interactive = group.interactable; Raycasts = group.blocksRaycasts; IgnoreParents = group.ignoreParentGroups; }
        }
        private static readonly ConditionalWeakTable<CanvasGroup, GroupDefaults> Defaults = new ConditionalWeakTable<CanvasGroup, GroupDefaults>();
        private readonly UIHost _host;
        private readonly bool _owned;
        private readonly CanvasGroup _group;
        private readonly GroupDefaults _default;
        private bool _disposed;
        private bool _lifetimeEnding;
        private readonly UIElement _lifetimeOwner;
        internal bool IsLifetimeEnding => _lifetimeEnding || (_lifetimeOwner?.IsLifetimeEnding ?? false);
        internal void BeginLifetimeDisposal() => _lifetimeEnding = true;
        internal bool DisplayWasDestroyed => !_disposed && IsLifetimeEnding && (!Rect);
        public UIElement(UIHost host, RectTransform rect, bool owned = false) : this(host, rect, owned, null) { }
        internal UIElement(UIHost host, RectTransform rect, bool owned, UIElement lifetimeOwner)
        {
            _lifetimeOwner = lifetimeOwner;
            _host = host ?? throw new ArgumentNullException(nameof(host));
            host.VerifyAccess();
            Rect = rect ? rect : throw new ArgumentNullException(nameof(rect));
            _owned = owned;
            _group = rect.GetComponent<CanvasGroup>();
            if (!_group) _group = rect.gameObject.AddComponent<CanvasGroup>();
            _default = Defaults.GetValue(_group, g => new GroupDefaults(g));
            foreach (var group in rect.GetComponentsInChildren<CanvasGroup>(true)) Defaults.GetValue(group, item => new GroupDefaults(item));
        }

        public IUINode GetNode(Type businessType)
        {
            VerifyAlive();
            var scripts = Rect.GetComponents<UINode>();
            if (scripts.Length != 1 || !businessType.IsInstanceOfType(scripts[0]))
                throw new InvalidOperationException("UI prefab root must contain exactly one matching UI script: " + businessType.FullName);
            return scripts[0];
        }
        public RectTransform Rect { get; }
        public UIHost Host => _host;
        private void VerifyAlive()
        {
            _host.VerifyAccess();
            if (_disposed || !Rect) throw new ObjectDisposedException(nameof(UIElement));
        }
        public void SetVisible(bool visible) { if (DisplayWasDestroyed) return; VerifyAlive(); Rect.gameObject.SetActive(visible); }
        public void SetInteractive(bool interactive)
        {
            if (DisplayWasDestroyed) return;
            VerifyAlive();
            foreach (var group in Rect.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.ignoreParentGroups = interactive && Defaults.GetValue(group, item => new GroupDefaults(item)).IgnoreParents;
            }
            _group.interactable = interactive && _default.Interactive;
            _group.blocksRaycasts = interactive && _default.Raycasts;
            // CanvasGroup gates input without overwriting a business Button.interactable value.
        }
        public void BringToFront() { if (DisplayWasDestroyed) return; VerifyAlive(); Rect.SetAsLastSibling(); }
        public object CaptureOrder() { VerifyAlive(); return Rect.GetSiblingIndex(); }
        public void RestoreOrder(object order)
        {
            if (DisplayWasDestroyed) return;
            VerifyAlive();
            if (!(order is int index)) throw new ArgumentException("Expected a sibling index.", nameof(order));
            if (index < 0 || index >= Rect.parent.childCount) throw new ArgumentOutOfRangeException(nameof(order));
            Rect.SetSiblingIndex(index);
        }
        public ValueTask DisposeAsync()
        {
            if (_disposed) return default;
            if (DisplayWasDestroyed) { _disposed = true; return default; }
            VerifyAlive();
            if (_owned)
            {
                Rect.gameObject.SetActive(false);
                Rect.SetParent(null, false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(Rect.gameObject); else UnityEngine.Object.DestroyImmediate(Rect.gameObject);
            }
            _disposed = true;
            return default;
        }
    }
}
