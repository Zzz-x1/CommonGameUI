using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo
{
[UIResource("res://Scenes/Client/Compare.tscn", WindowMode = WindowInstanceMode.Multiple)]
public partial class CompareView : EngineUI.UINode<CompareArgs>
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Comparison(Parameters.Position);
        var root = this.GetRoot();
        var card = root.GetNode<Control>("Card");
        card.AnchorLeft += Parameters.Position * .33f;
        card.AnchorRight += Parameters.Position * .33f;
        var item = Catalog.Item(Parameters.Item);
        root.GetNode<TextureRect>("Card/Icon").Texture = GD.Load<Texture2D>($"res://Assets/Items/{item.Icon}.svg");
        root.GetNode<Label>("Card/Info").Text = $"{item.Name}\n属性 +{item.Power}\n持有 {DemoApp.Instance.Store.State.Inventory[item.Id]}";
        root.GetNode<Button>("Card/Close").Pressed += Close;
        return Task.CompletedTask;
    }

    private void Close() => DemoApp.Instance.Track(CloseAsync());
    private async Task CloseAsync()
    {
        await DemoApp.Instance.Floating.CloseAsync(this);
        if (DemoApp.Instance.Shell.Pages.Current is ItemDetailsView item)
            item.Refresh();
    }

}
}
