using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo;
public sealed record ModalArgs(string Kind, int Id = 0, int Quantity = 1, string Text = "", IUINode? ReturnTo = null);
public sealed record RewardEntry(string Name, string Icon, int Count);
public sealed record PresentationArgs(string Text, int Hero = 0, RewardEntry[]? Rewards = null);
public sealed record CompareArgs(string Item, int Position);

/// <summary>Project timeline orchestration. AnimationPlayer tracks contain presentation only.</summary>
public sealed class CinematicTransition : IUITransition
{
    public IUITransitionRun Create(TransitionContext context)
    {
        var root = ((EngineUI.UIElement)context.Element!).Control;
        var player = root.GetNode<AnimationPlayer>("AnimationPlayer");
        var originals = new Dictionary<(Godot.Node, string), Variant>();
        foreach (var clip in new[]
        {
            "enter",
            "exit"
        })
        {
            var animation = player.GetAnimation(clip);
            for (int i = 0; i < animation.GetTrackCount(); i++)
            {
                var path = animation.TrackGetPath(i).ToString().Split(':', 2);
                var node = root.GetNode(path[0]);
                var key = (node, path[1]);
                originals.TryAdd(key, node.Get(path[1]));
            }
        }

        var scaled = originals.Keys.Where(k => k.Item2 == "scale").Select(k => (Control)k.Item1).ToArray();
        foreach (var control in scaled)
            originals.TryAdd((control, "pivot_offset"), control.PivotOffset);
        player.SpeedScale = DemoApp.Instance.Store.State.FullMotion ? 1 : 5;
        CpuParticles2D? particles = null;
        if (context.Node is PresentationView)
        {
            particles = root.GetNode<CpuParticles2D>("Center/Particles");
        }

        var inner = EngineUI.Transitions.Animation("AnimationPlayer", "enter", "exit", _ =>
        {
            foreach (var pair in originals)
                pair.Key.Item1.Set(pair.Key.Item2, pair.Value);
        }).Create(context);
        return new Run(inner, root, player, context.Entering, particles, scaled, (EngineUI.UIHost)context.Node.Context.Host);
    }

    private sealed class Run(IUITransitionRun inner, Control root, AnimationPlayer player, bool entering, CpuParticles2D? particles, Control[] scaled, EngineUI.UIHost host) : IUITransitionRun
    {
        private readonly Color _color = root.Modulate;
        private CancellationToken _token;
        public async Task PlayAsync(CancellationToken token)
        {
            _token = token;
            token.ThrowIfCancellationRequested();
            // Keep layout active, but do not draw the authored end state while it settles.
            if (entering)
                root.Modulate = new Color(_color.R, _color.G, _color.B, 0);
            await host.NextFrameAsync();
            token.ThrowIfCancellationRequested();
            foreach (var control in scaled)
                control.PivotOffset = control.Size / 2;
            if (particles != null)
            {
                particles.Position = particles.GetParent<Control>().Size / 2;
                particles.Emitting = entering && DemoApp.Instance.Store.State.FullMotion;
            }
            if (entering)
                DemoApp.Instance.Gate(() => player.Advance(player.GetAnimation("enter").Length - player.CurrentAnimationPosition + .01));
            try
            {
                root.Modulate = _color;
                await inner.PlayAsync(token);
            }
            finally
            {
                if (entering && GodotObject.IsInstanceValid(DemoApp.Instance))
                    DemoApp.Instance.Gate(null);
            }
        }

        public void Restore()
        {
            try
            {
                if (!_token.IsCancellationRequested || GodotObject.IsInstanceValid(root)) root.Modulate = _color;
                if (particles != null && (!_token.IsCancellationRequested || GodotObject.IsInstanceValid(particles))) particles.Emitting = false;
            }
            finally { inner.Restore(); }
        }
    }
}
