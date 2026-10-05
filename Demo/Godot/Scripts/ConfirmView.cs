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
    public string Message { get; set; } = "";
    private Control Root => ((EngineUI.UIElement)Element!).Control;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Popup("Confirm");
        foreach (var page in Root.GetNode<Control>("Center/Pages").GetChildren().Cast<Control>())
            page.Visible = page.Name == "Text";
        Root.GetNode<Label>("Center/Title").Text = "请确认";
        Root.GetNode<Label>("Center/Pages/Text/Body").Text = Message;
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

    private void Yes() => Complete(true);
    private void No() => Complete(false);
    protected override void OnEntered(LifecycleContext context) => Root.GetNode<Button>("Center/Confirm").GrabFocus();
}
}
