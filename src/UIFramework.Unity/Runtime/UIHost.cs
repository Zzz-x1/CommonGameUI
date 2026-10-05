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
        public UIHost(EventSystem events, FramePump frames)
        {
            _events = events ? events : throw new ArgumentNullException(nameof(events));
            _frames = frames ? frames : throw new ArgumentNullException(nameof(frames));
        }
        public void VerifyAccess()
        {
            if (Thread.CurrentThread.ManagedThreadId != _thread)
                throw new InvalidOperationException("UI access must run on Unity's main thread.");
        }
        public Task NextFrameAsync() { VerifyAccess(); return _frames.NextFrameAsync(); }
        public object CaptureFocus() { VerifyAccess(); VerifyEvents(); return _events.currentSelectedGameObject; }
        private void VerifyEvents() { if (!_events) throw new ObjectDisposedException(nameof(EventSystem)); }
        public void Focus(IUIElement element)
        {
            VerifyAccess();
            if (element == null) return;
            VerifyEvents();
            if (!(element is UIElement display)) throw new ArgumentException("Expected a Unity display.", nameof(element));
            foreach (var selectable in display.Rect.GetComponentsInChildren<Selectable>())
                if (selectable.isActiveAndEnabled && selectable.IsInteractable())
                { _events.SetSelectedGameObject(selectable.gameObject); return; }
            _events.SetSelectedGameObject(null);
        }
        public void RestoreFocus(object focus)
        {
            VerifyAccess();
            // Closing a page normally destroys its previous focus target.
            if (focus is GameObject target && target && target.activeInHierarchy
                && target.TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable())
            {
                if (_lifetimeEnding && !_events) return;
                VerifyEvents(); _events.SetSelectedGameObject(target);
            }
        }
        public void Error(Exception exception, string context)
        {
            Debug.LogError("[UI] " + context);
            Debug.LogException(exception);
        }
    }
}
