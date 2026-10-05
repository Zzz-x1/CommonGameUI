using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;

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
        var closeButton = this.GetControl<Button>("Content/Close");
        closeButton.Pressed += () => _observe(_windows.CloseAsync(this));
        return Task.CompletedTask;
    }
}
