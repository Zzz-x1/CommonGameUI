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
[UIResource("res://Scenes/Client/Lobby.tscn")]
public partial class LobbyView : ClientView
{
    private double _time;
    private int _event;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = new CinematicTransition();
        foreach (var name in new[]
        {
            "Tasks",
            "Events",
            "Friends",
            "Achievements",
            "Mail"
        })
            Wire(name == "Tasks" ? "Quest/Tasks" : name == "Events" ? "Event/Events" : name, () => App.OpenAsync(name));
        Wire("Welcome/NextHero", SwitchHeroAsync);
        Feedback();
        Refresh();
        return Task.CompletedTask;
    }

    public override void Refresh()
    {
        var hero = Catalog.Heroes[Data.Showcase];
        Get<TextureRect>("Hero").Texture = HeroTexture(Data.Showcase);
        Text("Welcome/HeroName", hero.Name + " · " + hero.Role);
        Text("Welcome/HeroStory", hero.Story);
        Text("Quest/QuestInfo", $"有 {Enumerable.Range(0, 12).Count(i => App.Store.CanClaim(false, i))} 项任务可以领取。\n整理装备，准备下一段旅程。");
        Get<Button>("Mail").Text = $"远方来信 · {Enumerable.Range(0, 8).Count(i => !Data.DeletedMail.Contains(i) && !Data.Claimed.Contains($"m{i}"))}";
    }

    public async Task SwitchHeroAsync()
    {
        var button = Get<Button>("Welcome/NextHero");
        button.Disabled = true;
        try
        {
            await App.RunAsync(this, async () =>
            {
                var portrait = Get<TextureRect>("Hero");
                var tween = Root.CreateTween();
                tween.TweenProperty(portrait, "modulate:a", 0, .18);
                await Root.ToSignal(tween, Tween.SignalName.Finished);
                App.Store.Change(s => s.Showcase = (s.Showcase + 1) % 6);
                Refresh();
                tween = Root.CreateTween();
                tween.TweenProperty(portrait, "modulate:a", 1, .28);
                await Root.ToSignal(tween, Tween.SignalName.Finished);
            });
        }
        finally
        {
            button.Disabled = false;
        }
    }

    public void Tick(double delta)
    {
        if (!IsActive || !Data.FullMotion)
            return;
        _time += delta;
        Get<TextureRect>("Hero").SelfModulate = new Color(1, 1, 1, .97f + (float)Math.Sin(_time) * .03f);
        if (_time > 6)
        {
            _time = 0;
            _event = (_event + 1) % 3;
            Text("Event/EventName", Catalog.Events[_event]);
        }
    }
}
}
