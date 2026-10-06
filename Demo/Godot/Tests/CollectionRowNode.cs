using UIFramework;
using UIFramework.Mvvm;
namespace HierarchyDemo.Tests;
public partial class CollectionRowNode : UIFramework.Godot.UINode
{
    public object ItemAtInitialize = null!; public object PlayerAtInitialize = null!;
    protected override async Task OnInitializeAsync(CancellationToken token)
    { ItemAtInitialize = this.GetViewModel<object>("Item"); PlayerAtInitialize = this.GetViewModel<object>("Player"); await ((UIFramework.Godot.UIHost)Context.Host).NextFrameAsync(); token.ThrowIfCancellationRequested(); }
}
