using Godot;

namespace UIFramework.Godot;

/// <summary>Offsets displace the authored anchored rectangle; relative offsets follow live size, scale/alpha multiply authored values.</summary>
public sealed record GodotTransitionFrame
{
    public Vector2 Offset { get; init; }
    public Vector2 RelativeOffset { get; init; }
    public Vector2 Scale { get; init; } = Vector2.One;
    public float Alpha { get; init; } = 1;
}

/// <summary>Immutable reusable recipes. Every operation captures independent presentation state.</summary>
public static class GodotTransitions
{
    public static IUITransition Fade(double duration = 0.15, string? targetPath = null, double delay = 0,
        Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut) =>
        GodotUI.Configure(() => new Recipe(duration, delay, targetPath, transition, ease, Effect.Fade));
    public static IUITransition SlideHorizontal(double duration = 0.20, string? targetPath = null, double delay = 0,
        Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut) =>
        GodotUI.Configure(() => new Recipe(duration, delay, targetPath, transition, ease, Effect.Slide));
    public static IUITransition ScaleFade(double duration = 0.16, float fromScale = 0.94f, string? targetPath = null,
        double delay = 0, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut) =>
        GodotUI.Configure(() => new Recipe(duration, delay, targetPath, transition, ease, Effect.Scale, fromScale));

    /// <summary>Explicit start/end frames, independent of entering/exiting; use Transitions.Select to choose them.</summary>
    public static IUITransition Simple(GodotTransitionFrame from, GodotTransitionFrame to, double duration = 0.20,
        double delay = 0, string? targetPath = null, Tween.TransitionType transition = Tween.TransitionType.Cubic,
        Tween.EaseType ease = Tween.EaseType.InOut) =>
        GodotUI.Configure(() => new Recipe(duration, delay, targetPath, transition, ease, Effect.Custom, from: from, to: to));

    /// <summary>Project restoration must restore all animation-owned tracks; method/audio tracks remain project responsibilities.</summary>
    public static IUITransition Animation(string playerPath, string enterAnimation, string exitAnimation,
        Action<Control> restore) => GodotUI.Configure(() => new AnimationRecipe(playerPath, enterAnimation, exitAnimation, restore));

    private enum Effect { Fade, Slide, Scale, Custom }
    private static Control Display(TransitionContext context) => context.Element is GodotUIElement element
        ? element.Control : throw new ArgumentException("A Godot transition requires a Godot display.");
    private static void ValidateFrame(GodotTransitionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!frame.Offset.IsFinite() || !frame.RelativeOffset.IsFinite() || !frame.Scale.IsFinite()
            || !float.IsFinite(frame.Alpha) || frame.Alpha < 0 || frame.Alpha > 1)
            throw new ArgumentOutOfRangeException(nameof(frame), "Frame values must be finite and alpha within 0..1.");
    }

    private sealed class Recipe : IUITransition
    {
        private readonly double _duration, _delay;
        private readonly string? _path;
        private readonly Effect _effect;
        private readonly float _scale;
        private readonly Tween.TransitionType _transition;
        private readonly Tween.EaseType _ease;
        private readonly GodotTransitionFrame? _from, _to;
        public Recipe(double duration, double delay, string? path, Tween.TransitionType transition, Tween.EaseType ease,
            Effect effect, float scale = 1, GodotTransitionFrame? from = null, GodotTransitionFrame? to = null)
        {
            if (!double.IsFinite(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (!double.IsFinite(delay) || delay < 0 || !double.IsFinite(duration + delay)) throw new ArgumentOutOfRangeException(nameof(delay));
            if (!float.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (!Enum.IsDefined(transition)) throw new ArgumentOutOfRangeException(nameof(transition));
            if (!Enum.IsDefined(ease)) throw new ArgumentOutOfRangeException(nameof(ease));
            if (path != null && string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Target path cannot be empty.", nameof(path));
            if (effect == Effect.Custom) { ValidateFrame(from!); ValidateFrame(to!); }
            _duration = duration; _delay = delay; _path = path; _effect = effect; _scale = scale;
            _transition = transition; _ease = ease; _from = from; _to = to;
        }
        public IUITransitionRun Create(TransitionContext context)
        {
            var root = Display(context);
            var target = _path == null ? root : root.GetNode<Control>(_path)
                ?? throw new KeyNotFoundException($"Animation target '{_path}' was not found.");
            var neutral = new GodotTransitionFrame();
            var away = _effect switch
            {
                Effect.Fade => neutral with { Alpha = 0 },
                Effect.Scale => neutral with { Alpha = 0, Scale = Vector2.One * _scale },
                Effect.Slide => SlideFrame(context),
                _ => neutral
            };
            var from = _effect == Effect.Custom ? _from! : context.Entering ? away : neutral;
            var to = _effect == Effect.Custom ? _to! : context.Entering ? neutral : away;
            return new Run(target, context, _duration, _delay, _transition, _ease, from, to);
        }
        private static GodotTransitionFrame SlideFrame(TransitionContext context)
        {
            if (context.Direction == TransitionDirection.None)
                throw new InvalidOperationException("SlideHorizontal requires a stack forward/backward operation.");
            var sign = context.Direction == TransitionDirection.Forward ? 1f : -1f;
            if (!context.Entering) sign = -sign;
            return new GodotTransitionFrame { RelativeOffset = new Vector2(sign, 0) };
        }
    }
    private sealed class Run : IUITransitionRun
    {
        private readonly Control _target;
        private readonly TransitionContext _context;
        private readonly double _duration, _delay;
        private readonly Tween.TransitionType _transition;
        private readonly Tween.EaseType _ease;
        private readonly GodotTransitionFrame _from, _to;
        private readonly bool _move, _resize, _fade;
        private readonly Color _color;
        private readonly Vector2 _scale, _pivot;
        private readonly Vector4 _offsets;
        public Run(Control target, TransitionContext context, double duration, double delay,
            Tween.TransitionType transition, Tween.EaseType ease, GodotTransitionFrame from, GodotTransitionFrame to)
        {
            _target = target; _context = context; _duration = duration; _delay = delay; _transition = transition; _ease = ease;
            _from = from; _to = to;
            _move = from.Offset != Vector2.Zero || to.Offset != Vector2.Zero || from.RelativeOffset != Vector2.Zero || to.RelativeOffset != Vector2.Zero;
            _resize = from.Scale != Vector2.One || to.Scale != Vector2.One;
            _fade = from.Alpha != 1 || to.Alpha != 1;
            if ((_move || _resize) && target.GetParent() is global::Godot.Container)
                throw new InvalidOperationException("Sliding/scaling requires a wrapper outside a Godot layout Container; specify targetPath.");
            _color = target.Modulate; _scale = target.Scale; _pivot = target.PivotOffset;
            _offsets = new Vector4(target.OffsetLeft, target.OffsetTop, target.OffsetRight, target.OffsetBottom);
        }
        private void SetOffset(Vector2 offset)
        {
            // Preserve the anchored rectangle, including layout changes while a newly loaded Container settles.
            _target.OffsetLeft = _offsets.X + offset.X; _target.OffsetTop = _offsets.Y + offset.Y;
            _target.OffsetRight = _offsets.Z + offset.X; _target.OffsetBottom = _offsets.W + offset.Y;
        }
        private void Apply(double elapsed)
        {
            var active = Math.Max(0, elapsed - _delay);
            var t = elapsed < _delay ? 0 : _duration == 0 ? 1 : Math.Min(1, active / _duration);
            var eased = t is 0 or 1 ? (float)t : Tween.InterpolateValue(0f, 1f, active, _duration, _transition, _ease).AsSingle();
            if (_fade) _target.Modulate = new Color(_color.R, _color.G, _color.B, _color.A * Mathf.Lerp(_from.Alpha, _to.Alpha, eased));
            if (_move)
            {
                var from = _from.Offset + _from.RelativeOffset * _target.Size;
                var to = _to.Offset + _to.RelativeOffset * _target.Size;
                SetOffset(from.Lerp(to, eased));
            }
            if (_resize) { _target.PivotOffset = _target.Size / 2; _target.Scale = _scale * _from.Scale.Lerp(_to.Scale, eased); }
        }
        public async Task PlayAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Apply(0); _context.ReportProgress(0);
            var total = _duration + _delay;
            var tree = _target.GetTree();
            var elapsed = 0d;
            while (elapsed < total)
            {
                await _target.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                token.ThrowIfCancellationRequested();
                elapsed += _target.GetProcessDeltaTime();
                Apply(Math.Min(elapsed, total));
                _context.ReportProgress((float)Math.Min(1, elapsed / total));
            }
            Apply(total); _context.ReportProgress(1);
        }
        public void Restore()
        {
            if (!GodotObject.IsInstanceValid(_target) || _target.IsQueuedForDeletion()) throw new ObjectDisposedException("Animation target");
            if (_fade) _target.Modulate = _color;
            if (_move) SetOffset(Vector2.Zero);
            if (_resize) { _target.Scale = _scale; _target.PivotOffset = _pivot; }
        }
    }
    private sealed class AnimationRecipe : IUITransition
    {
        private readonly string _path, _enter, _exit;
        private readonly Action<Control> _restore;
        public AnimationRecipe(string path, string enter, string exit, Action<Control> restore)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(enter) || string.IsNullOrWhiteSpace(exit))
                throw new ArgumentException("Animation player path and clip names are required.");
            _path = path; _enter = enter; _exit = exit; _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        }
        public IUITransitionRun Create(TransitionContext context)
        {
            var root = Display(context);
            var player = root.GetNode<AnimationPlayer>(_path) ?? throw new KeyNotFoundException(_path);
            var name = context.Entering ? _enter : _exit;
            if (!player.HasAnimation(name)) throw new KeyNotFoundException($"Animation '{name}' was not found.");
            if (player.GetAnimation(name).LoopMode != global::Godot.Animation.LoopModeEnum.None)
                throw new InvalidOperationException("Transition animations cannot loop.");
            if (!double.IsFinite(player.GetAnimation(name).Length) || player.GetAnimation(name).Length < 0)
                throw new InvalidOperationException("Transition animation length must be finite and nonnegative.");
            if (!double.IsFinite(player.SpeedScale) || player.SpeedScale <= 0)
                throw new InvalidOperationException("Transition AnimationPlayer speed must be finite and positive.");
            if (player.IsPlaying()) throw new InvalidOperationException("AnimationPlayer is already playing.");
            return new AnimationRun(root, player, name, _restore, context, player.GetAnimation(name).Length);
        }
    }
    private sealed class AnimationRun(Control root, AnimationPlayer player, string name, Action<Control> restore,
        TransitionContext context, double length) : IUITransitionRun
    {
        public async Task PlayAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            context.ReportProgress(0);
            var finished = false;
            void Finished(StringName clip) { if (clip == name) finished = true; }
            player.AnimationFinished += Finished;
            Exception? primary = null;
            try
            {
                player.Play(name);
                // Play schedules evaluation; sample now before the first visible draw.
                player.Advance(0);
                while (!finished)
                {
                    await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
                    token.ThrowIfCancellationRequested();
                    if (finished) break;
                    if (!player.IsPlaying() || player.CurrentAnimation != name)
                        throw new InvalidOperationException("Transition animation was stopped or replaced during playback.");
                    if (!double.IsFinite(player.SpeedScale) || player.SpeedScale <= 0)
                        throw new InvalidOperationException("Transition AnimationPlayer speed must remain finite and positive.");
                    context.ReportProgress(length > 0 ? (float)Math.Min(1, player.CurrentAnimationPosition / length) : 0);
                }
                context.ReportProgress(1);
            }
            catch (Exception ex) { primary = ex; throw; }
            finally { GodotUI.Cleanup(primary, () => player.AnimationFinished -= Finished); }
        }
        public void Restore()
        {
            Exception? primary = null;
            try { player.Stop(keepState: true); }
            catch (Exception ex) { GodotUI.Report(ex, "Stop transition animation"); primary = ex; }
            GodotUI.Cleanup(primary, () => restore(root));
            if (primary != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }
}
