using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UnityEngine;
using UnityEngine.UI;
using EngineUI = global::UIFramework.Unity;

public sealed class HelloWindow : EngineUI.UINode
{
    [SerializeField] private Button closeButton = null;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        closeButton.onClick.AddListener(() => Observe(((PanelContainerNode)Parent).CloseAsync(this)));
        return Task.CompletedTask;
    }
}
