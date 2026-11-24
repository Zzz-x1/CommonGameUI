using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UIFramework.Unity
{
    public sealed class UIHost : IUIHost, IUILog
    {
        private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
        private readonly EventSystem _events;
        private readonly FramePump _frames;
        private bool _lifetimeEnding;
        internal void BeginLifetimeDisposal() => _lifetimeEnding = true;
        public RectTransform RootMount { get; }
        public UIHost(EventSystem events, FramePump frames, RectTransform rootMount = null)
        {
            RootMount = rootMount;
            _events = events ? events : throw new ArgumentNullException(nameof(events));
            _frames = frames ? frames : throw new ArgumentNullException(nameof(frames));
        }
        public void VerifyAccess()
        {
            if (Thread.CurrentThread.ManagedThreadId != _thread)
                throw new InvalidOperationException("UI access must run on Unity's main thread.");
        }
        public Task NextFrameAsync() { VerifyAccess(); return _frames.NextFrameAsync(); }
        public object CaptureFocus()
        {
            VerifyAccess();
            if (_lifetimeEnding && !_events) return null;
            VerifyEvents();
            var target = _events.currentSelectedGameObject;
            return target && target.activeInHierarchy && (!target.TryGetComponent<Selectable>(out var control)
                || control.isActiveAndEnabled && control.IsInteractable()) ? target : null;
        }
        private void VerifyEvents() { if (!_events) throw new ObjectDisposedException(nameof(EventSystem)); }
        private static GameObject FocusObject(object target) =>
            target is Selectable selectable && selectable ? selectable.gameObject : target as GameObject;
        public bool ContainsFocus(IUIElement scope, object target)
        {
            VerifyAccess();
            if (scope is not UIElement display || display.Host != this) return false;
            var rect = display.Rect;
            var candidate = FocusObject(target);
            return rect && candidate && candidate.transform.IsChildOf(rect);
        }
        public bool TryFocus(IUIElement scope, object target)
        {
            VerifyAccess();
            if (_lifetimeEnding && !_events) return false;
            VerifyEvents();
            if (scope != null && (!(scope is UIElement display) || display.Host != this)) return false;
            var root = scope == null ? RootMount : ((UIElement)scope).Rect;
            if (scope != null && !root) return false;
            if (target != null)
            {
                var candidate = FocusObject(target);
                if (!candidate || (root && !candidate.transform.IsChildOf(root))
                    || !candidate.TryGetComponent<Selectable>(out var button) || !button.isActiveAndEnabled || !button.IsInteractable()) return false;
                _events.SetSelectedGameObject(candidate);
                return _events.currentSelectedGameObject == candidate;
            }
            if (scope == null) return false;
            foreach (var button in root.GetComponentsInChildren<Selectable>())
                if (button.isActiveAndEnabled && button.IsInteractable())
                { _events.SetSelectedGameObject(button.gameObject); return _events.currentSelectedGameObject == button.gameObject; }
            return false;
        }
        public void ClearFocus()
        {
            VerifyAccess();
            if (_lifetimeEnding && !_events) return;
            VerifyEvents();
            _events.SetSelectedGameObject(null);
        }
        public void Error(string message) => Debug.LogError("[UI] " + message);
        public void Exception(Exception exception, string context)
        {
            Error(context);
            Debug.LogException(exception);
        }
    }
}
