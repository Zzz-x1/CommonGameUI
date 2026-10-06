using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using System.Collections.ObjectModel;
using Godot;
using UIFramework;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;
namespace HierarchyDemo;
[ViewModel]
public class ClientInventoryVm
{
    [FieldNotify] public ObservableCollection<ClientInventoryItemVm> Equipment { get; set; } = new();
    [FieldNotify] public ObservableCollection<ClientInventoryItemVm> Materials { get; set; } = new();
    public void Update(ObservableCollection<ClientInventoryItemVm> list, int[] ids, Func<int, ClientInventoryItemVm> create, Action<ClientInventoryItemVm> refresh)
    {
        foreach (var item in list.Where(item => !ids.Contains(item.Id)).ToArray()) list.Remove(item);
        for (int i = 0; i < ids.Length; i++)
        {
            var item = list.FirstOrDefault(item => item.Id == ids[i]);
            if (item == null) { item = create(ids[i]); list.Insert(i, item); }
            else { var current = list.IndexOf(item); if (current != i) list.Move(current, i); }
            refresh(item);
        }
    }
}
[ViewModel]
public class ClientInventoryItemVm
{
    public int Id { get; set; }
    [FieldNotify] public string Title { get; set; } = "";
    [FieldNotify] public string Info { get; set; } = "";
    [FieldNotify] public Texture2D Icon { get; set; } = null!;
    public Func<Task> Open { get; set; } = null!;
    [Command] public Task Select() => Open();
}
// The project owns its four-column layout; the module owns instances and bindings.
internal sealed class InventoryGridTarget : ICollectionTarget, IAsyncDisposable
{
    private readonly EngineMvvm.CollectionTarget _items;
    private readonly Control _mount;
    public InventoryGridTarget(IUINode owner, Control mount, PackedScene row) { _mount = mount; _items = new(owner, mount, row); _items.Changed += Arrange; }
    public void Insert(int index, object item) { _items.Insert(index, item); Arrange(); }
    public void Remove(int index) { _items.Remove(index); Arrange(); }
    public void Move(int oldIndex, int newIndex) { _items.Move(oldIndex, newIndex); Arrange(); }
    public void Clear() => _items.Clear();
    public void Dispose() { _items.Changed -= Arrange; _items.Dispose(); }
    public ValueTask DisposeAsync() { _items.Changed -= Arrange; return _items.DisposeAsync(); }
    private void Arrange()
    {
        var rows = _mount.GetChildren().Cast<Control>().ToArray();
        for (int i = 0; i < rows.Length; i++) rows[i].Name = "MovingRow" + i;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i]; row.Name = "C" + i;
            row.AnchorLeft = .006f + .25f * (i % 4); row.AnchorRight = row.AnchorLeft + .238f;
            row.AnchorTop = .012f + i / 4 / 3f; row.AnchorBottom = row.AnchorTop + .3093333f;
            row.OffsetLeft = row.OffsetRight = row.OffsetTop = row.OffsetBottom = 0;
        }
    }
}
