using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;

// A project animation, deliberately built only on the existing Source/Target contract.
public sealed class ItemPartnerTransition : IUITransition
{
    public const double Duration = .48;
    public IUITransitionRun Create(TransitionContext context)
    {
        var details = (DetailsView)context.Node;
        var partner = ReferenceEquals(context.Node, context.Source) ? context.Target : context.Source;
        var inventory = (InventoryView)partner!;
        return new Run(context, details.GetControl(), inventory.SelectedIcon,
            details.GetControl<TextureRect>("Artwork/Icon"), context.Container!.GetControl());
    }

    private sealed class Run : IUITransitionRun
    {
        private readonly TransitionContext _context;
        private readonly Control _page, _mount;
        private readonly TextureRect _small, _large;
        private readonly Color _pageColor, _smallColor, _largeColor;
        private readonly TextureRect _flying;

        public Run(TransitionContext context, Control page, TextureRect small, TextureRect large, Control mount)
        {
            _context = context; _page = page; _small = small; _large = large; _mount = mount;
            _pageColor = page.Modulate; _smallColor = small.SelfModulate; _largeColor = large.SelfModulate;
            _flying = new TextureRect
            {
                Name = "FlyingItemPortrait", Texture = small.Texture, Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 100,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale, SelfModulate = _largeColor
            };
            _mount.AddChild(_flying);
        }

        private Rect2 Rectangle(Control icon)
        {
            var transform = _mount.GetGlobalTransformWithCanvas().AffineInverse() * icon.GetGlobalTransformWithCanvas();
            var from = transform * Vector2.Zero;
            var to = transform * icon.Size;
            var size = to - from;
            return new Rect2(from, size);
        }

        private void Apply(float progress)
        {
            // Sample live rectangles so resizing does not leave an outdated destination.
            var small = Rectangle(_small); var large = Rectangle(_large);
            var start = _context.Entering ? small : large;
            var end = _context.Entering ? large : small;
            var eased = 1 - Mathf.Pow(1 - progress, 3);
            _flying.Position = start.Position.Lerp(end.Position, eased);
            _flying.Size = start.Size.Lerp(end.Size, eased);
            var pageAlpha = _context.Entering
                ? Mathf.SmoothStep(0, 1, Mathf.Clamp((progress - .3f) / .7f, 0, 1))
                : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp(progress / .3f, 0, 1));
            _page.Modulate = new Color(_pageColor.R, _pageColor.G, _pageColor.B, _pageColor.A * pageAlpha);
        }

        public async Task PlayAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var origin = Rectangle(_context.Entering ? _small : _large);
            _flying.Position = origin.Position;
            _flying.Size = origin.Size;
            _flying.Show();
            // Alpha keeps layout active. Visibility=false would collapse a Container child.
            _small.SelfModulate = new Color(_smallColor.R, _smallColor.G, _smallColor.B, 0);
            _large.SelfModulate = new Color(_largeColor.R, _largeColor.G, _largeColor.B, 0);
            if (_context.Entering) _page.Modulate = new Color(_pageColor.R, _pageColor.G, _pageColor.B, 0);
            var tree = _page.GetTree();
            // A newly shown VBoxContainer needs its deferred layout pass before geometry sampling.
            await _page.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await _page.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            token.ThrowIfCancellationRequested();
            Apply(0); _context.ReportProgress(0);
            var elapsed = 0d;
            while (elapsed < Duration)
            {
                await _page.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                token.ThrowIfCancellationRequested();
                elapsed += _page.GetProcessDeltaTime();
                var progress = (float)Math.Min(1, elapsed / Duration);
                Apply(progress); _context.ReportProgress(progress);
            }
        }

        public void Restore()
        {
            // No task/signal survives the run. Detach immediately, free on Godot's deferred queue.
            _mount.RemoveChild(_flying);
            _flying.QueueFree();
            _page.Modulate = _pageColor;
            _small.SelfModulate = _smallColor;
            _large.SelfModulate = _largeColor;
        }
    }
}
