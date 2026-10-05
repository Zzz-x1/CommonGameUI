using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using EngineUI = global::UIFramework.Godot;

public partial class HelloWindow : EngineUI.UINode
{
    [Export] public Button CloseButton { get; set; } = null!;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        CloseButton.Pressed += () => Observe(((PanelContainerNode)Parent!).CloseAsync(this));
        return Task.CompletedTask;
    }
}
