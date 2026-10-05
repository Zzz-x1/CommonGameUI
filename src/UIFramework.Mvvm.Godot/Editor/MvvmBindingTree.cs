using Godot;
namespace UIFramework.Mvvm.Godot;
[Tool]
public partial class MvvmBindingTree : Tree
{
    public event Action<string>? Dropped;
    public override bool _CanDropData(Vector2 position, Variant data) => data.VariantType == Variant.Type.String;
    public override void _DropData(Vector2 position, Variant data) => Dropped?.Invoke(data.AsString());
}
