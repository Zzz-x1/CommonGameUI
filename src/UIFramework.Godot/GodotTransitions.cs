using Godot;

namespace UIFramework.Godot;

/// <summary>Reusable recipes. Every operation captures independent presentation state.</summary>
public static class GodotTransitions
{
    public static IUITransition Fade(double duration = 0.15, string? targetPath = null) =>
        GodotUI.Configure(() => new Recipe(duration, targetPath, Effect.Fade, 1));
    public static IUITransition SlideHorizontal(double duration = 0.20, string? targetPath = null) =>
        GodotUI.Configure(() => new Recipe(duration, targetPath, Effect.Slide, 1));
    public static IUITransition ScaleFade(double duration = 0.16, float fromScale = 0.94f, string? targetPath = null) =>
        GodotUI.Configure(() => new Recipe(duration, targetPath, Effect.Scale, fromScale));

    /// <summary>Project restoration must restore all animation-owned tracks; method/audio tracks remain project responsibilities.</summary>
    public static IUITransition Animation(string playerPath, string enterAnimation, string exitAnimation,
        Action<Control> restore) => GodotUI.Configure(() => new AnimationRecipe(playerPath, enterAnimation, exitAnimation, restore));

    private enum Effect { Fade, Slide, Scale }
    private static Control Display(TransitionContext context) => context.Element is GodotUIElement element
        ? element.Control : throw new ArgumentException("A Godot transition requires a Godot display.");

    private sealed class Recipe : IUITransition
    {
        private readonly double _duration;
        private readonly string? _path;
        private readonly Effect _effect;
        private readonly float _scale;
        public Recipe(double duration, string? path, Effect effect, float scale)
        {
            if (!double.IsFinite(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (!float.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (path != null && string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Target path cannot be empty.", nameof(path));
            _duration = duration; _path = path; _effect = effect; _scale = scale;
        }
        public IUITransitionRun Create(TransitionContext context)
        {
            var root = Display(context);
            var target = _path == null ? root : root.GetNode<Control>(_path)
                ?? throw new KeyNotFoundException($"Animation target '{_path}' was not found.");
            if (_effect != Effect.Fade && target.GetParent() is global::Godot.Container)
                throw new InvalidOperationException("Sliding/scaling requires a wrapper outside a Godot layout Container; specify targetPath.");
            if (_effect == Effect.Slide && context.Direction == TransitionDirection.None)
                throw new InvalidOperationException("SlideHorizontal requires a stack forward/backward operation.");
            return new Run(target, context, _duration, _effect, _scale);
        }
    }
    private sealed class Run : IUITransitionRun
    {
        private readonly Control _target;
        private readonly TransitionContext _context;
        private readonly double _duration;
        private readonly Effect _effect;
        private readonly float _fromScale;
        private readonly Color _color;
        private readonly Vector2 _position, _scale, _pivot;
        public Run(Control target, TransitionContext context, double duration, Effect effect, float scale)
        {
            _target = target; _context = context; _duration = duration; _effect = effect; _fromScale = scale;
            _color = target.Modulate; _position = target.Position; _scale = target.Scale; _pivot = target.PivotOffset;
        }
        private void Apply(float progress)
        {
            var amount = _context.Entering ? 1 - progress : progress;
            if (_effect is Effect.Fade or Effect.Scale)
                _target.Modulate = new Color(_color.R, _color.G, _color.B, _color.A * (1 - amount));
            if (_effect == Effect.Scale)
            {
                _target.PivotOffset = _target.Size / 2;
                _target.Scale = _scale * Mathf.Lerp(1, _fromScale, amount);
            }
            if (_effect == Effect.Slide)
            {
                var sign = _context.Direction == TransitionDirection.Forward ? 1f : -1f;
                if (!_context.Entering) sign = -sign;
                _target.Position = _position + new Vector2(sign * _target.Size.X * amount, 0);
            }
        }
        public async Task PlayAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Apply(0);
            var tree = _target.GetTree();
            var elapsed = 0d;
            while (elapsed < _duration)
            {
                await _target.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                token.ThrowIfCancellationRequested();
                elapsed += _target.GetProcessDeltaTime();
                var t = (float)Math.Min(1, elapsed / _duration);
                Apply(t * t * (3 - 2 * t));
            }
            Apply(1);
        }
        public void Restore()
        {
            if (!GodotObject.IsInstanceValid(_target) || _target.IsQueuedForDeletion()) throw new ObjectDisposedException("Animation target");
            if (_effect is Effect.Fade or Effect.Scale) _target.Modulate = _color;
            if (_effect == Effect.Slide) _target.Position = _position;
            if (_effect == Effect.Scale) { _target.Scale = _scale; _target.PivotOffset = _pivot; }
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
            if (player.IsPlaying()) throw new InvalidOperationException("AnimationPlayer is already playing.");
            return new AnimationRun(root, player, name, _restore);
        }
    }
    private sealed class AnimationRun(Control root, AnimationPlayer player, string name, Action<Control> restore) : IUITransitionRun
    {
        public async Task PlayAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            player.Play(name);
            while (player.IsPlaying())
            {
                await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
                token.ThrowIfCancellationRequested();
            }
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
