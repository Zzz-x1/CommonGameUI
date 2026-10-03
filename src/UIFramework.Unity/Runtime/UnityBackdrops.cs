using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UIFramework.Unity
{
    public static class UnityBackdrops
    {
        public static IUIBackdropFactory Dim(Action<Task> observe, Color? color = null, double duration = .15) =>
            UnityUI.Configure<IUIBackdropFactory>(() => new Factory(observe, color ?? new Color(.04f, .07f, .12f, .7f), duration));
        private sealed class Factory : IUIBackdropFactory
        {
            private readonly Action<Task> _observe;
            private readonly Color _color;
            private readonly double _duration;
            public Factory(Action<Task> observe, Color color, double duration)
            {
                _observe = observe ?? throw new ArgumentNullException(nameof(observe));
                if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
                _color = color; _duration = duration;
            }
            public Task<IUIBackdrop> CreateAsync(IUIElement mount, Func<Task> requestDismiss, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var target = mount as UnityUIElement ?? throw new ArgumentException("Backdrop requires a Unity mount.");
                target.Host.VerifyAccess();
                var go = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
                try
                {
                    var rect = (RectTransform)go.transform;
                    rect.SetParent(target.Rect, false);
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                    go.GetComponent<Image>().color = _color;
                    var button = go.GetComponent<Button>();
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
                    button.onClick.AddListener(() => _observe(requestDismiss()));
                    go.SetActive(false);
                    return Task.FromResult<IUIBackdrop>(new Backdrop(new UnityUIElement(target.Host, rect, true), _duration));
                }
                catch (Exception ex) { UnityUI.Cleanup(ex, () => { go.transform.SetParent(null); UnityEngine.Object.Destroy(go); }); throw; }
            }
        }
        private sealed class Backdrop : IUIBackdrop
        {
            private readonly UnityUIElement _display;
            private readonly CanvasGroup _group;
            private readonly double _duration;
            public Backdrop(UnityUIElement display, double duration)
            { _display = display; _group = display.Rect.GetComponent<CanvasGroup>(); _duration = duration; }
            public Task ShowAsync(bool playAnimation, CancellationToken token) => ChangeAsync(true, playAnimation, token);
            public Task HideAsync(bool playAnimation, CancellationToken token) => ChangeAsync(false, playAnimation, token);
            private async Task ChangeAsync(bool show, bool animate, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var wasVisible = _display.Rect.gameObject.activeSelf;
                var original = _group.alpha;
                var start = Time.realtimeSinceStartupAsDouble;
                Exception primary = null;
                try
                {
                    _display.SetVisible(true);
                    float progress;
                    do
                    {
                        token.ThrowIfCancellationRequested();
                        progress = !animate || _duration == 0 ? 1 : (float)Math.Min(1, (Time.realtimeSinceStartupAsDouble - start) / _duration);
                        _group.alpha = show ? progress : 1 - progress;
                        if (progress < 1) await _display.Host.NextFrameAsync();
                    } while (progress < 1);
                    _display.SetVisible(show);
                }
                catch (Exception ex)
                {
                    primary = ex;
                    UnityUI.Cleanup(ex, () => _display.SetVisible(wasVisible));
                    throw;
                }
                finally { UnityUI.Cleanup(primary, () => _group.alpha = original); }
            }
            public void Restore(bool visible) => _display.SetVisible(visible);
            public void SetVisible(bool visible) => _display.SetVisible(visible);
            public void SetInteractive(bool interactive) => _display.SetInteractive(interactive);
            public void BringToFront() => _display.BringToFront();
            public object CaptureOrder() => _display.CaptureOrder();
            public void RestoreOrder(object order) => _display.RestoreOrder(order);
            public ValueTask DisposeAsync() => _display.DisposeAsync();
        }
    }
}
