using System;
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UnityEngine.UI;

public sealed class HelloWindow : UINode
{
    private readonly PanelContainerNode _windows;
    private readonly Action<Task> _observe;

    public HelloWindow(PanelContainerNode windows, Action<Task> observe)
    {
        _windows = windows;
        _observe = observe;
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        var closeButton = this.GetComponent<Button>("Content/Close");
        closeButton.onClick.AddListener(() => _observe(_windows.CloseAsync(this)));
        return Task.CompletedTask;
    }
}
