using System.Threading;
using System.Threading.Tasks;
using EngineUI = UIFramework.Unity;
namespace UIFramework.Tests.Unity
{
    public sealed class NativeParameterWindow : EngineUI.UINode<string>
    {
        public string InitializedWith;
        protected override Task OnInitializeAsync(CancellationToken token)
        { InitializedWith = Parameters; return Task.CompletedTask; }
    }
}
