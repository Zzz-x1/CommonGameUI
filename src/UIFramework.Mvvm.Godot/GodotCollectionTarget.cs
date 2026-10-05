using Godot;
using UIFramework.Godot;
namespace UIFramework.Mvvm.Godot;
public sealed class GodotCollectionTarget : ICollectionTarget
{
    private readonly UINode _owner; private readonly Control _mount; private readonly PackedScene _template;
    private readonly List<(Control Control, UINode Node)> _items = new();
    private int _id;
    public GodotCollectionTarget(UINode owner, Control mount, PackedScene template) { _owner = owner; _mount = mount; _template = template; }
    public void Insert(int index, object item)
    {
        var control = _template.Instantiate<Control>(); _mount.AddChild(control);
        var node = new ItemNode(item); var attach = node.AttachToAsync(_owner, "Item" + _id++, control);
        if (!attach.IsCompleted) throw new InvalidOperationException("List item initialization must be synchronous."); attach.GetAwaiter().GetResult();
        index = index == int.MaxValue ? _items.Count : index; _items.Insert(index, (control, node)); _mount.MoveChild(control, index);
    }
    public void Remove(int index)
    {
        var item = _items[index]; _items.RemoveAt(index);
        if (GodotObject.IsInstanceValid(item.Control)) item.Control.GetParent().RemoveChild(item.Control);
        _owner.ObserveMvvmTask(Release());
        async Task Release()
        {
            try { await item.Node.DisposeAsync(); }
            finally { if (GodotObject.IsInstanceValid(item.Control)) item.Control.QueueFree(); }
        }
    }
    public void Move(int oldIndex, int newIndex) { var item = _items[oldIndex]; _items.RemoveAt(oldIndex); _items.Insert(newIndex, item); _mount.MoveChild(item.Control, newIndex); }
    public void Clear() { for (int i = _items.Count - 1; i >= 0; i--) Remove(i); }
    public void Dispose() => Clear();
    private sealed class ItemNode(object vm) : UINode
    { protected override bool InitiallyVisible => true;
        protected override Task OnInitializeAsync(CancellationToken token) { this.SetViewModel("Item", vm); return Task.CompletedTask; } }
}
