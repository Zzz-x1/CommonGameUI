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
public partial class ConfirmView : EngineUI.DialogNode<bool>
{
    public string Message { get; private set; } = "";
    protected override void OnSetParameters(object? args) => Message = args is string message
        ? message : throw new ArgumentException("Confirmation requires a message.", nameof(args));
    private Control Root => ((EngineUI.UIElement)Element!).Control;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Popup("Confirm");
        foreach (var page in Root.GetNode<Control>("Center/Pages").GetChildren().Cast<Control>())
            page.Visible = page.Name == "Text";
        Root.GetNode<Label>("Center/Title").Text = "请确认";
        Root.GetNode<Button>("Center/Confirm").Pressed += Yes;
        Root.GetNode<Button>("Center/Cancel").Pressed += No;
        Root.GetNode<Button>("Center/Close").Pressed += No;
        DismissRequested = _ =>
        {
            Complete(false);
            return Task.CompletedTask;
        };
        return Task.CompletedTask;
    }

    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        Root.GetNode<Label>("Center/Pages/Text/Body").Text = Message;
        return Task.CompletedTask;
    }

    private void Yes() => Complete(true);
    private void No() => Complete(false);
    protected override void OnEntered(LifecycleContext context) => Root.GetNode<Button>("Center/Confirm").GrabFocus();
}
}
