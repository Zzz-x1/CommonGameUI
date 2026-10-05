using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;

// Project art direction. Recipes use existing framework contracts and engine effects.
public static class ClientTransitions
{
    public static IUITransition Page(string module) => Transitions.Select(context =>
    {
        if (module == "Inventory" && (context.Source is ItemDetailsView || context.Target is ItemDetailsView))
            return EngineUI.Transitions.Fade(.12);
        if (!DemoApp.Instance.Store.State.FullMotion)
            return EngineUI.Transitions.Fade(.1);
        if (context.Phase is TransitionPhase.PopEnter or TransitionPhase.PopExit)
            return EngineUI.Transitions.SlideHorizontal(.24);
        if (context.Entering && context.Source is LobbyView)
            return new CinematicTransition();
        if (context.Source is ClientPage source && context.Target is ClientPage target)
        {
            var direction = ModulePosition(target.Module) > ModulePosition(source.Module) ? 1 : -1;
            var away = new EngineUI.TransitionFrame { RelativeOffset = new Vector2(direction * (context.Entering ? .2f : -.2f), 0), Alpha = 0 };
            var neutral = new EngineUI.TransitionFrame();
            return EngineUI.Transitions.Simple(context.Entering ? away : neutral, context.Entering ? neutral : away, .3);
        }
        return context.Entering ? new CinematicTransition() : EngineUI.Transitions.SlideHorizontal(.24);
    });

    private static int ModulePosition(string module) => module switch
    {
        "Characters" => 0, "Inventory" => 1, "Team" => 2, "Stages" => 3, "Shop" => 4,
        "Tasks" => 5, "Achievements" => 6, "Mail" => 7, "Friends" => 8, "Events" => 9,
        _ => throw new ArgumentOutOfRangeException(nameof(module), module, "Unknown client module.")
    };

    public static IUITransition Presentation() => Transitions.Select(context =>
    {
        if (!context.Entering)
            return EngineUI.Transitions.ScaleFade(.18, targetPath: "Center");
        if (context.Node is RewardView && context.Source is ClientModalView { Parameters.Kind: "Mail" })
            return DemoApp.Instance.Store.State.FullMotion
                ? new GildedRevealTransition(0, .45, "Center")
                : EngineUI.Transitions.Fade(.1, "Center");
        return new CinematicTransition();
    });

    public static IUITransition Popup(string kind) => Transitions.Select(context =>
    {
        if (!DemoApp.Instance.Store.State.FullMotion)
            return EngineUI.Transitions.ScaleFade(.1, targetPath: "Center");
        if (context.Entering && kind is "Mail" or "Shop" or "Purchase")
            return new GildedRevealTransition(kind == "Mail" ? 0 : 1, .38, "Center");
        var neutral = new EngineUI.TransitionFrame();
        var away = kind switch
        {
            "Profile" => neutral with { Scale = Vector2.One * .72f, Alpha = 0 },
            "Stage" or "Departure" => neutral with { Offset = new Vector2(120, 0), Alpha = 0 },
            _ => neutral with { Offset = new Vector2(0, 60), Scale = Vector2.One * .9f, Alpha = 0 }
        };
        return EngineUI.Transitions.Simple(context.Entering ? away : neutral, context.Entering ? neutral : away,
            context.Entering ? .3 : .18, targetPath: "Center", transition: Tween.TransitionType.Back, ease: Tween.EaseType.Out);
    });

    public static IUITransition Category(string module) => Transitions.Select(context =>
        DemoApp.Instance.Store.State.FullMotion
            ? new GildedRevealTransition(module == "Shop" ? 1 : module == "Friends" ? 2 : 0, context.Entering ? .32 : .18)
            : EngineUI.Transitions.Fade(.1));

    public static IUITransition Hero() => Transitions.Select(context =>
        DemoApp.Instance.Store.State.FullMotion
            ? new GildedRevealTransition(2, context.Entering ? .45 : .22)
            : EngineUI.Transitions.Fade(.1));

    public static IUITransition Comparison(int position) => Transitions.Select(context =>
    {
        var neutral = new EngineUI.TransitionFrame();
        var away = neutral with { RelativeOffset = new Vector2(position == 0 ? -.35f : .35f, 0), Scale = Vector2.One * .85f, Alpha = 0 };
        return EngineUI.Transitions.Simple(context.Entering ? away : neutral, context.Entering ? neutral : away,
            DemoApp.Instance.Store.State.FullMotion ? .3 : .1, targetPath: "Card", transition: Tween.TransitionType.Back, ease: Tween.EaseType.Out);
    });
}

public sealed class GildedRevealTransition(int style, double duration, string? targetPath = null) : IUITransition
{
    public IUITransitionRun Create(TransitionContext context)
    {
        var root = ((EngineUI.UIElement)context.Element!).Control;
        return new Run(context, targetPath == null ? root : root.GetNode<Control>(targetPath), style, duration);
    }

    private sealed class Run : IUITransitionRun
    {
        private readonly TransitionContext _context;
        private readonly Control _root;
        private readonly double _duration;
        private readonly Dictionary<CanvasItem, Material?> _originals;
        private readonly ShaderMaterial _material;

        public Run(TransitionContext context, Control root, int style, double duration)
        {
            _context = context;
            _root = root;
            _duration = duration;
            _originals = ClientView.Descendants(root).Prepend(root).OfType<CanvasItem>().ToDictionary(c => c, c => (Material?)c.Material);
            _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/GildedReveal.gdshader") };
            _material.SetShaderParameter("style", style);
        }

        private CancellationToken _token;
        public async Task PlayAsync(CancellationToken token)
        {
            _token = token;
            token.ThrowIfCancellationRequested();
            _material.SetShaderParameter("progress", _context.Entering ? 0f : 1f);
            foreach (var item in _originals.Keys)
                item.Material = _material;
            await ((EngineUI.UIHost)_context.Node.Context.Host).NextFrameAsync();
            token.ThrowIfCancellationRequested();
            var viewport = _root.GetViewport().GetVisibleRect().Size;
            var rect = _root.GetGlobalRect();
            _material.SetShaderParameter("panel_origin", rect.Position / viewport);
            _material.SetShaderParameter("panel_extent", rect.Size / viewport);
            var started = Time.GetTicksUsec();
            float progress;
            do
            {
                token.ThrowIfCancellationRequested();
                progress = (float)Math.Min(1, (Time.GetTicksUsec() - started) / 1_000_000d / _duration);
                var eased = Mathf.SmoothStep(0, 1, progress);
                _material.SetShaderParameter("progress", _context.Entering ? eased : 1 - eased);
                _context.ReportProgress(progress);
                if (progress < 1)
                    await ((EngineUI.UIHost)_context.Node.Context.Host).NextFrameAsync();
            } while (progress < 1);
        }

        public void Restore()
        {
            try
            {
                foreach (var pair in _originals)
                {
                    if (_token.IsCancellationRequested && !GodotObject.IsInstanceValid(pair.Key)) continue;
                    pair.Key.Material = pair.Value;
                }
            }
            finally { _material.Dispose(); }
        }
    }
}
