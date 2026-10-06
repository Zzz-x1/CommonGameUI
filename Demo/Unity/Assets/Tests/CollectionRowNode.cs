using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Mvvm;
namespace StarCourt.UnityDemo.Tests
{
    public sealed class CollectionRowNode : UIFramework.Unity.UINode
    {
        public object ItemAtInitialize; public object PlayerAtInitialize;
        protected override async Task OnInitializeAsync(CancellationToken token)
        { ItemAtInitialize = this.GetViewModel<object>("Item"); PlayerAtInitialize = this.GetViewModel<object>("Player"); await ((UIFramework.Unity.UIHost)Context.Host).NextFrameAsync(); token.ThrowIfCancellationRequested(); }
    }
}
