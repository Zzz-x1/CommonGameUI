using Godot;
namespace UIFramework.Mvvm.Godot;
[Tool]
public partial class MvvmMemberTree : Tree
{
    public override Variant _GetDragData(Vector2 position)
    { var row = GetItemAtPosition(position); if (row == null) return default; var path = row.GetMetadata(0); SetDragPreview(new Label { Text = path.AsString() }); return path; }
}
