using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Unity
{
    public sealed class UnityTransitionFrame
    {
        public Vector2 Offset { get; }
        public Vector2 RelativeOffset { get; }
        public float Scale { get; }
        public float Alpha { get; }
        public UnityTransitionFrame(Vector2 offset = default, Vector2 relativeOffset = default, float scale = 1, float alpha = 1)
        { Offset = offset; RelativeOffset = relativeOffset; Scale = scale; Alpha = alpha; }
    }
    public static class UnityTransitions
    {
        public static IUITransition Fade(double duration = .18, string targetPath = null) =>
            Transitions.Select(c => Simple(c.Entering ? new UnityTransitionFrame(alpha: 0) : new UnityTransitionFrame(),
                c.Entering ? new UnityTransitionFrame() : new UnityTransitionFrame(alpha: 0), duration, targetPath));
        public static IUITransition ScaleFade(double duration = .22, string targetPath = null) =>
            Transitions.Select(c => Simple(c.Entering ? new UnityTransitionFrame(scale: .85f, alpha: 0) : new UnityTransitionFrame(),
                c.Entering ? new UnityTransitionFrame() : new UnityTransitionFrame(scale: .85f, alpha: 0), duration, targetPath));
        public static IUITransition SlideHorizontal(double duration = .28, string targetPath = null) =>
            Transitions.Select(c =>
            {
                var sign = c.Direction == TransitionDirection.Backward ? -1 : 1;
                var away = new UnityTransitionFrame(relativeOffset: new Vector2(sign * (c.Entering ? 1 : -1), 0));
                return Simple(c.Entering ? away : new UnityTransitionFrame(), c.Entering ? new UnityTransitionFrame() : away, duration, targetPath);
            });
        public static IUITransition Simple(UnityTransitionFrame from, UnityTransitionFrame to, double duration = .25, string targetPath = null, AnimationCurve easing = null) =>
            UnityUI.Configure<IUITransition>(() =>
            {
                if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
                Validate(from); Validate(to);
                return new Recipe(from, to, duration, targetPath, easing ?? AnimationCurve.EaseInOut(0, 0, 1, 1));
            });
        private static void Validate(UnityTransitionFrame frame)
        {
            if (!Finite(frame.Offset.x) || !Finite(frame.Offset.y) || !Finite(frame.RelativeOffset.x) || !Finite(frame.RelativeOffset.y)
                || !Finite(frame.Scale) || frame.Scale <= 0 || !Finite(frame.Alpha) || frame.Alpha < 0 || frame.Alpha > 1)
                throw new ArgumentOutOfRangeException(nameof(frame));
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static RectTransform Target(TransitionContext context, string path)
        {
            var root = context.Node.GetRect();
            if (path == null) return root;
            var target = root.Find(path) as RectTransform;
            return target ? target : throw new InvalidOperationException("Transition RectTransform not found: " + path);
        }
        private sealed class Recipe : IUITransition
        {
            private readonly UnityTransitionFrame _from, _to;
            private readonly double _duration;
            private readonly string _path;
            private readonly AnimationCurve _easing;
            public Recipe(UnityTransitionFrame from, UnityTransitionFrame to, double duration, string path, AnimationCurve easing)
            { _from = from; _to = to; _duration = duration; _path = path; _easing = easing; }
            public IUITransitionRun Create(TransitionContext context) => new Run(context, Target(context, _path), _from, _to, _duration, _easing);
        }
        private sealed class Run : IUITransitionRun
        {
            private readonly TransitionContext _context;
            private readonly RectTransform _target;
            private readonly CanvasGroup _group;
            private readonly UnityTransitionFrame _from, _to;
            private readonly Vector2 _position;
            private readonly Vector3 _scale;
            private readonly float _alpha;
            private readonly double _duration;
            private readonly AnimationCurve _easing;
            public Run(TransitionContext context, RectTransform target, UnityTransitionFrame from, UnityTransitionFrame to, double duration, AnimationCurve easing)
            {
                _context = context; _target = target; _from = from; _to = to; _duration = duration; _easing = easing;
                _group = target.GetComponent<CanvasGroup>();
                if (!_group) _group = target.gameObject.AddComponent<CanvasGroup>();
                _position = target.anchoredPosition; _scale = target.localScale; _alpha = _group.alpha;
            }
            public async Task PlayAsync(CancellationToken token)
            {
                var host = (UnityUIHost)_context.Node.Context.Host;
                await host.NextFrameAsync();
                token.ThrowIfCancellationRequested();
                var size = _target.rect.size;
                var start = Time.realtimeSinceStartupAsDouble;
                float progress;
                do
                {
                    token.ThrowIfCancellationRequested();
                    progress = _duration == 0 ? 1 : (float)Math.Min(1, (Time.realtimeSinceStartupAsDouble - start) / _duration);
                    var eased = _easing.Evaluate(progress);
                    var from = _from.Offset + Vector2.Scale(_from.RelativeOffset, size);
                    var to = _to.Offset + Vector2.Scale(_to.RelativeOffset, size);
                    _target.anchoredPosition = _position + Vector2.LerpUnclamped(from, to, eased);
                    _target.localScale = _scale * Mathf.LerpUnclamped(_from.Scale, _to.Scale, eased);
                    _group.alpha = _alpha * Mathf.LerpUnclamped(_from.Alpha, _to.Alpha, eased);
                    _context.ReportProgress(progress);
                    if (progress < 1) await host.NextFrameAsync();
                } while (progress < 1);
            }
            public void Restore() { _target.anchoredPosition = _position; _target.localScale = _scale; _group.alpha = _alpha; }
        }
        public static IUITransition Animation(string playerPath, string enterClip, string exitClip, Action<RectTransform> restore) =>
            UnityUI.Configure<IUITransition>(() => new ClipRecipe(playerPath, enterClip, exitClip, restore ?? throw new ArgumentNullException(nameof(restore))));
        private sealed class ClipRecipe : IUITransition
        {
            private readonly string _path, _enter, _exit;
            private readonly Action<RectTransform> _restore;
            public ClipRecipe(string path, string enter, string exit, Action<RectTransform> restore)
            { _path = path; _enter = enter; _exit = exit; _restore = restore; }
            public IUITransitionRun Create(TransitionContext context)
            {
                var root = Target(context, _path);
                var player = root.GetComponent<Animation>();
                if (!player) throw new InvalidOperationException("Animation component is required.");
                var name = context.Entering ? _enter : _exit;
                var clip = player.GetClip(name);
                if (!clip || !clip.legacy || clip.isLooping) throw new InvalidOperationException("Expected a non-looping legacy UI AnimationClip: " + name);
                return new ClipRun(context, root, player, name, clip.length, _restore);
            }
        }
        private sealed class ClipRun : IUITransitionRun
        {
            private readonly TransitionContext _context;
            private readonly RectTransform _root;
            private readonly Animation _player;
            private readonly string _name;
            private readonly float _length;
            private readonly Action<RectTransform> _restore;
            private readonly float _speed, _time;
            private readonly WrapMode _wrap;
            public ClipRun(TransitionContext context, RectTransform root, Animation player, string name, float length, Action<RectTransform> restore)
            {
                _context = context; _root = root; _player = player; _name = name; _length = length; _restore = restore;
                var state = player[name]; _speed = state.speed; _time = state.time; _wrap = state.wrapMode;
            }
            public async Task PlayAsync(CancellationToken token)
            {
                var host = (UnityUIHost)_context.Node.Context.Host;
                token.ThrowIfCancellationRequested();
                _player[_name].wrapMode = WrapMode.ClampForever;
                _player[_name].speed = 0;
                _player.Play(_name);
                _player[_name].time = 0;
                _player.Sample();
                _context.ReportProgress(0);
                var started = Time.realtimeSinceStartupAsDouble;
                float elapsed;
                do
                {
                    token.ThrowIfCancellationRequested();
                    if (!_player.IsPlaying(_name)) throw new InvalidOperationException("UI AnimationClip stopped before completion: " + _name);
                    elapsed = (float)Math.Min(_length, Time.realtimeSinceStartupAsDouble - started);
                    _player[_name].time = elapsed; _player.Sample();
                    _context.ReportProgress(_length == 0 ? 1 : elapsed / _length);
                    if (elapsed < _length) await host.NextFrameAsync();
                } while (elapsed < _length);
            }
            public void Restore()
            {
                Exception primary = null;
                try
                {
                    _player.Stop(_name);
                    var state = _player[_name]; state.speed = _speed; state.time = _time; state.wrapMode = _wrap;
                }
                catch (Exception ex) { primary = ex; throw; }
                finally { UnityUI.Cleanup(primary, () => _restore(_root)); }
            }
        }
    }
}

