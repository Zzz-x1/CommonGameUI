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
public abstract partial class PresentationView : EngineUI.UINode<PresentationArgs>
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Presentation();
        this.GetControl<Label>("Center/Result").Text = Parameters.Text;
        if (this is TrainingView)
            this.GetControl<TextureRect>("Center/Portrait").Texture = GD.Load<Texture2D>($"res://Assets/Client/hero{Parameters.Hero}.png");
        if (this is RewardView)
        {
            var rewards = Parameters.Rewards ?? Array.Empty<RewardEntry>();
            for (int i = 0; i < 3; i++)
            {
                var card = this.GetControl<Control>($"Center/Cards/C{i}");
                card.Visible = i < rewards.Length;
                if (i >= rewards.Length)
                    continue;
                card.AnchorLeft = (float)i / rewards.Length + .02f;
                card.AnchorRight = (float)(i + 1) / rewards.Length - .02f;
                card.GetNode<TextureRect>("Icon").Texture = GD.Load<Texture2D>($"res://Assets/Items/{rewards[i].Icon}.svg");
                card.GetNode<Label>("Name").Text = $"{rewards[i].Name}\n× {rewards[i].Count}";
            }
        }

        this.GetControl<Button>("Center/Close").Pressed += Close;
        DismissRequested = _ => DemoApp.Instance.Modals.CloseAsync(this);
        return Task.CompletedTask;
    }

    private void Close() => DemoApp.Instance.Track(DemoApp.Instance.Modals.CloseAsync(this));
}
}
