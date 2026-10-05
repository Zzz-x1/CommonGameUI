using Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;
namespace HierarchyDemo;
public partial class MvvmRowAuthoring : global::Godot.Node
{
    public override void _Ready()
    {
        var source = GD.Load<PackedScene>("res://Scenes/Client/Inventory.tscn").Instantiate<Control>();
        var row = (Control)source.GetNode<Control>("Pages/装备/C0").Duplicate(); row.Name = "Card";
        foreach (var child in ClientView.Descendants(row)) child.Owner = row;
        var config = new MvvmBindings { Name = "MvvmBindings" }; row.AddChild(config); config.Owner = row;
        config.AddBinding(row.GetNode<Control>("Title"), new ViewBindingResource { Member = "Text", Source = "Item.Title" });
        config.AddBinding(row.GetNode<Control>("Info"), new ViewBindingResource { Member = "Text", Source = "Item.Info" });
        config.AddBinding(row.GetNode<Control>("Icon"), new ViewBindingResource { Member = "Texture", Source = "Item.Icon" });
        config.AddBinding(row, new ViewBindingResource { Kind = ViewBindingKind.Command, Source = "Item.Select" });
        var scene = new PackedScene(); if (scene.Pack(row) != Error.Ok) throw new InvalidOperationException("Cannot pack inventory row.");
        if (ResourceSaver.Save(scene, "res://Scenes/Client/InventoryRow.tscn") != Error.Ok) throw new IOException("Cannot save inventory row.");
        row.Free(); source.Free(); GD.Print("MVVM inventory row authored"); GetTree().Quit();
    }
}
