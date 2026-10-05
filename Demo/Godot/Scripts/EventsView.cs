using Transitions = global::UIFramework.Transitions;
using UINode = global::UIFramework.UINode;
using EngineMvvm = global::UIFramework.Mvvm.Godot;
using EngineUI = global::UIFramework.Godot;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo
{
[UIResource("res://Scenes/Client/Events.tscn")]
public partial class EventsView : ClientView
{
    public TabContainerNode Tabs { get; private set; } = null!;

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Transition = new CinematicTransition();
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Targets", Get<Control>("Pages"), token);
        string[] names =
        {
            "庆典",
            "签到",
            "采购"
        };
        for (int i = 0; i < 3; i++)
        {
            int id = i;
            var n = names[i];
            await Tabs.BindAsync(n, Get<Control>("Pages/" + n), token);
            Wire("Tabs/" + n, () => SelectAsync(n));
            Wire("Pages/" + n + "/Claim", () => ClaimAsync(id));
        }

        Wire("Fold", FoldAsync);
        await Tabs.SwitchAsync("庆典", false, token);
        Feedback();
        Refresh();
    }

    public Task SelectAsync(string name) => Tabs.CurrentName == name ? Task.CompletedTask :
        App.RunAsync(this, () => Tabs.SwitchAsync(name));
    public Task FoldAsync() => App.RunAsync(this, () => Tabs.Current == null ? Tabs.SwitchAsync("庆典") : Tabs.HideAsync());
    public async Task ClaimAsync(int id)
    {
        App.Store.ClaimEvent(id);
        Refresh();
        await App.RewardAsync("活动奖励已领取", new("星石", "diamond-ring", 50), new("经验手记", Catalog.Item("mat6").Icon, 3));
    }

    public override void Refresh()
    {
        string[] names =
        {
            "庆典",
            "签到",
            "采购"
        };
        for (int i = 0; i < 3; i++)
        {
            var b = Get<Button>("Pages/" + names[i] + "/Claim");
            b.Disabled = !App.Store.CanClaimEvent(i);
            b.Text = Data.Claimed.Contains($"e{i}") ? "已领取" : i == 2 && Data.Counts["purchase"] == 0 ? "先完成一次购买" : "领取奖励";
        }
    }
}
}
