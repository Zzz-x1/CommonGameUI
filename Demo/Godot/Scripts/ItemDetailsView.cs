using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo
{
[UIResource("res://Scenes/Client/ItemDetails.tscn", CacheMode = NodeCacheMode.KeepAlive)]
public partial class ItemDetailsView : EngineUI.UINode<string>
{
    private DemoApp App => DemoApp.Instance;
    private Control Root => ((EngineUI.UIElement)Element!).Control;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = Transitions.Select(c => c.Source is InventoryView || c.Target is InventoryView ? new ItemPartnerTransition() : EngineUI.Transitions.SlideHorizontal(.24));
        Root.GetNode<Button>("Favorite").Pressed += Favorite;
        Root.GetNode<Button>("Compare").Pressed += Compare;
        var vm = this.CreateViewModel<ClientItemVm>("Item"); vm.Initialize(App.Store, Parameters);
        this.Bind("Item.Name", Root.GetNode<Label>("Name").TextTarget());
        this.Bind("Item.Description", Root.GetNode<Label>("Description").TextTarget());
        this.Bind("Item.Count", Root.GetNode<Label>("Count").TextTarget());
        this.Bind("Item.Icon", EngineMvvm.BindingTargets.Property(Root.GetNode<TextureRect>("Icon"), "Texture"));
        Refresh();
        return Task.CompletedTask;
    }

    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        this.GetViewModel<ClientItemVm>("Item").SelectItem(Parameters);
        Refresh();
        return Task.CompletedTask;
    }

    private void Favorite()
    {
        App.Store.Favorite(Parameters);
        Refresh();
    }

    private void Compare() => App.Track(CompareAsync());
    public async Task CompareAsync()
    {
        if (App.Floating.Children.Count >= 2)
            throw new InvalidOperationException("对比最多两件装备。");
        if (App.Floating.Children.Count == 0)
            await App.Floating.OpenAsync<CompareView>(new CompareArgs(Parameters, 0));
        else
            await App.ModalAsync(new ModalArgs("Compare"));
        Refresh();
    }

    protected override void OnActivated(StateChangeContext context) => Refresh();
    public void Refresh()
    {
        var item = Catalog.Item(Parameters);
        Root.GetNode<Button>("Favorite").Text = App.Store.State.Favorites.Contains(Parameters) ? "取消收藏" : "收藏";
        Root.GetNode<Button>("Compare").Disabled = item.Power == 0 || App.Floating.Children.Count >= 2;
    }

}
}
