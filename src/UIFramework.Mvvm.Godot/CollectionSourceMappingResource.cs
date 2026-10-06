using Godot;
namespace UIFramework.Mvvm.Godot;
[Tool, GlobalClass]
public partial class CollectionSourceMappingResource : Resource
{
    [Export] public string ParentSource { get; set; } = "";
    [Export] public string ItemSource { get; set; } = "";
    public CollectionSourceMapping ToMapping() => new() { ParentSource = ParentSource, ItemSource = ItemSource };
}
