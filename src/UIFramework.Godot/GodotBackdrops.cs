using Godot;

namespace UIFramework.Godot;

public static class GodotBackdrops
{
    /// <summary>observe must retain and inspect each task, including failures after the backdrop is freed.</summary>
    public static IUIBackdropFactory Dim(Action<Task> observe, Color? color = null, double duration = 0.12) =>
        GodotUI.Configure(() => new Factory(observe, color ?? new Color(0.08f, 0.14f, 0.13f, 0.68f), duration));

    private sealed class Factory : IUIBackdropFactory
    {
        private readonly Action<Task> _observe;
        private readonly Color _color;
        private readonly double _duration;
        public Factory(Action<Task> observe, Color color, double duration)
        {
            _observe = observe ?? throw new ArgumentNullException(nameof(observe));
            if (!double.IsFinite(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (!float.IsFinite(color.R) || !float.IsFinite(color.G) || !float.IsFinite(color.B) || !float.IsFinite(color.A)
                || color.A < 0 || color.A > 1) throw new ArgumentOutOfRangeException(nameof(color));
            _color = color; _duration = duration;
        }
        public Task<IUIBackdrop> CreateAsync(IUIElement? mount, Func<Task> requestDismiss, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (mount is not GodotUIElement target) throw new ArgumentException("Backdrop requires a Godot mount.");
            var control = new ColorRect { Name = "ModalBackdrop", Color = _color, MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
            try
            {
                target.Control.AddChild(control);
                control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                return Task.FromResult<IUIBackdrop>(new Backdrop(control, _duration, requestDismiss, _observe));
            }
            catch { control.Free(); throw; }
        }
    }
    private sealed class Backdrop : IUIBackdrop
    {
        private readonly GodotUIElement _display;
        private readonly double _duration;
        private readonly Func<Task> _dismiss;
        private readonly Action<Task> _observe;
        private readonly ColorRect _control;
        private bool _interactive;
        public Backdrop(ColorRect control, double duration, Func<Task> dismiss, Action<Task> observe)
        {
            _control = control; _display = new GodotUIElement(control, owned: true);
            _duration = duration; _dismiss = dismiss; _observe = observe;
            control.GuiInput += Input;
        }
        private void Input(InputEvent input)
        {
            if (_interactive && input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                try
                {
                    _control.AcceptEvent();
                    // The observer belongs to the application, not this soon-to-be-disposed display.
                    _observe(_dismiss());
                }
                catch (Exception ex) { GodotUI.Report(ex, "Observe backdrop dismiss request"); throw; }
            }
        }
        public Task ShowAsync(bool playAnimation, CancellationToken token) => ChangeAsync(true, playAnimation, token);
        public Task HideAsync(bool playAnimation, CancellationToken token) => ChangeAsync(false, playAnimation, token);
        private Task ChangeAsync(bool show, bool playAnimation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (playAnimation) return AnimateAsync(show, token);
            _display.SetVisible(show);
            return Task.CompletedTask;
        }
        private async Task AnimateAsync(bool show, CancellationToken token)
        {
            var wasVisible = _control.Visible;
            var original = _control.Modulate;
            Exception? primary = null;
            try
            {
                _control.Visible = true;
                var elapsed = 0d;
                void Apply(float t) => _control.Modulate = new Color(original.R, original.G, original.B, original.A * (show ? t : 1 - t));
                Apply(0);
                while (elapsed < _duration)
                {
                    await _control.ToSignal(_control.GetTree(), SceneTree.SignalName.ProcessFrame);
                    token.ThrowIfCancellationRequested();
                    elapsed += _control.GetProcessDeltaTime(); Apply((float)Math.Min(1, elapsed / _duration));
                }
                token.ThrowIfCancellationRequested();
                _control.Visible = show;
            }
            catch (Exception ex)
            {
                primary = ex;
                GodotUI.Cleanup(ex, () => _control.Visible = wasVisible);
                throw;
            }
            finally { GodotUI.Cleanup(primary, () => _control.Modulate = original); }
        }
        public void SetVisible(bool visible) => _display.SetVisible(visible);
        public void Restore(bool visible) { _control.Modulate = Colors.White; _display.SetVisible(visible); }
        public void SetInteractive(bool interactive)
        {
            _interactive = interactive;
            _display.SetInteractive(interactive);
            // Always block the mouse; disabling the dismiss action must not let input pass through.
            _control.MouseFilter = Control.MouseFilterEnum.Stop;
        }
        public void BringToFront() => _display.BringToFront();
        public object CaptureOrder() => _display.CaptureOrder();
        public void RestoreOrder(object order) => _display.RestoreOrder(order);
        public ValueTask DisposeAsync() { _control.GuiInput -= Input; return _display.DisposeAsync(); }
    }
}
