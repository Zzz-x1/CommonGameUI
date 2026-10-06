using Godot;
using UIFramework.Godot;
using CoreNode = global::UIFramework.UINode;
namespace UIFramework.Mvvm.Godot;
public sealed class CollectionTarget : CollectionTargetBase
{
    private readonly IUINode _owner; private readonly Control _mount; private readonly PackedScene? _template;
    private readonly bool _preview; private readonly ICollectionItemFactory? _factory;
    private readonly IReadOnlyList<CollectionSourceMapping> _sources; private int _id;
    private readonly string _name = "Items_" + Guid.NewGuid().ToString("N");
    public CollectionTarget(IUINode owner, Control mount, PackedScene? template, bool preview = false,
        ICollectionItemFactory? factory = null, IReadOnlyList<CollectionSourceMapping>? sources = null) : base(owner)
    { _owner = owner; _mount = mount; _template = template; _preview = preview; _factory = factory; _sources = sources ?? Array.Empty<CollectionSourceMapping>(); if (template == null && factory == null) throw new ArgumentException("An item resource or factory is required."); }
    protected override global::UIFramework.Mvvm.CollectionItemContext CreateContext(object item, int index) =>
        new NativeItemContext(_owner, item, index, _preview, _mount, _sources, _name + "_" + _id++);
    protected override Task<IUINode> CreateItemAsync(global::UIFramework.Mvvm.CollectionItemContext context, CancellationToken token) =>
        _factory == null ? context.CreateAsync(_template!, cancellationToken: token) : _factory.CreateAsync(context, token);
    protected override void Place(IUINode node, int index) { var control = ((UIElement)node.Element!).Control; _mount.MoveChild(control, index); }
    protected override void Detach(IUINode node) { var control = ((UIElement)node.Element!).Control; control.Hide(); control.GetParent().RemoveChild(control); }
}
internal sealed class NativeItemContext : global::UIFramework.Mvvm.CollectionItemContext
{
    internal NativeItemContext(IUINode owner, object item, int index, bool preview, Control mount,
        IReadOnlyList<CollectionSourceMapping> sources, string name) : base(owner, item, index, preview, new UIElement(mount), sources, name) { }
    internal Task<IUINode> CreateAsync(PackedScene scene, Action<IUINode>? configure, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var control = scene.Instantiate<Control>(); control.Hide(); ((UIElement)Mount).Control.AddChild(control);
        return AttachAsync(new UIElement(control, owned: true), configure, token);
    }
    protected override IUINode ResolveNode(IUIElement element) =>
        !IsPreview && ((UIElement)element).Control is IUINode node ? node : new CoreNode();
}
public static class CollectionItemExtensions
{
    public static Task<IUINode> CreateAsync(this global::UIFramework.Mvvm.CollectionItemContext context, PackedScene scene,
        Action<IUINode>? configure = null, CancellationToken cancellationToken = default) => ((NativeItemContext)context).CreateAsync(scene, configure, cancellationToken);
}
