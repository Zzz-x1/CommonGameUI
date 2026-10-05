using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace UIFramework.Unity
{
    public static partial class UnityTransitions
    {
        public static IUITransition Timeline(string enterDirectorPath, string exitDirectorPath, Action<RectTransform> restore) =>
            UnityUI.Configure<IUITransition>(() => new TimelineRecipe(enterDirectorPath, exitDirectorPath, restore ?? throw new ArgumentNullException(nameof(restore))));

        private sealed class TimelineRecipe : IUITransition
        {
            private readonly string _enter, _exit;
            private readonly Action<RectTransform> _restore;
            public TimelineRecipe(string enter, string exit, Action<RectTransform> restore)
            { _enter = enter; _exit = exit; _restore = restore; }
            public IUITransitionRun Create(TransitionContext context)
            {
                var root = context.Node.GetRect(); var path = context.Entering ? _enter : _exit;
                var target = path == null ? root : root.Find(path);
                if (!target) throw new InvalidOperationException("Timeline director path not found: " + path);
                var director = target.GetComponent<PlayableDirector>();
                if (!director || !director.enabled || !director.playableAsset) throw new InvalidOperationException("An enabled PlayableDirector with an asset is required.");
                if (director.playOnAwake) throw new InvalidOperationException("UI Timeline requires Play On Awake to be disabled.");
                if (director.playableGraph.IsValid()) throw new InvalidOperationException("UI Timeline director is already in use.");
                if (double.IsNaN(director.duration) || double.IsInfinity(director.duration) || director.duration < 0)
                    throw new InvalidOperationException("UI Timeline requires a finite duration.");
                return new TimelineRun(context, root, director, _restore);
            }
        }
        private sealed class TimelineRun : IUITransitionRun
        {
            private readonly TransitionContext _context;
            private readonly RectTransform _root;
            private readonly PlayableDirector _director;
            private readonly PlayableAsset _asset;
            private readonly Action<RectTransform> _restore;
            private readonly DirectorUpdateMode _mode;
            private readonly DirectorWrapMode _wrap;
            private readonly double _time, _duration;
            private bool _started, _stopped;
            public TimelineRun(TransitionContext context, RectTransform root, PlayableDirector director, Action<RectTransform> restore)
            {
                _context = context; _root = root; _director = director; _asset = director.playableAsset; _restore = restore;
                _mode = director.timeUpdateMode; _wrap = director.extrapolationMode; _time = director.time; _duration = director.duration;
            }
            private void Stopped(PlayableDirector director) => _stopped = true;
            public async Task PlayAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                _director.stopped += Stopped; _started = true;
                _director.timeUpdateMode = DirectorUpdateMode.Manual;
                _director.extrapolationMode = DirectorWrapMode.Hold;
                _director.time = 0; _director.Play(); _director.Evaluate();
                _context.ReportProgress(0);
                var host = (UnityUIHost)_context.Node.Context.Host;
                var started = Time.realtimeSinceStartupAsDouble;
                double elapsed;
                do
                {
                    token.ThrowIfCancellationRequested();
                    if (_stopped || !_director.playableGraph.IsValid() || _director.state != PlayState.Playing || _director.playableAsset != _asset)
                        throw new InvalidOperationException("UI Timeline playback was interrupted.");
                    elapsed = Math.Min(_duration, Time.realtimeSinceStartupAsDouble - started);
                    _director.time = elapsed; _director.Evaluate();
                    _context.ReportProgress(_duration == 0 ? 1 : (float)(elapsed / _duration));
                    if (elapsed < _duration) await host.NextFrameAsync();
                } while (elapsed < _duration);
            }
            public void Restore()
            {
                if (Destroyed(_context, _root)) return;
                Exception first = null;
                try
                {
                    _director.stopped -= Stopped;
                    if (_started) _director.Stop();
                    _director.timeUpdateMode = _mode; _director.extrapolationMode = _wrap; _director.time = _time;
                }
                catch (Exception error) { first = error; throw; }
                finally { UnityUI.Cleanup(first, () => _restore(_root)); }
            }
        }
    }
}
