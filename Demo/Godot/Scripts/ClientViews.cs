using Godot;
using UIFramework;
using UIFramework.Godot;

namespace HierarchyDemo;
public abstract class ClientView : UINode
{
    protected DemoApp App => DemoApp.Instance;
    protected PlayerState Data => App.Store.State;
    protected Control Root => ((GodotUIElement)Element!).Control;

    private readonly List<Action> _disconnect = new();
    private readonly Dictionary<Button, Tween> _feedback = new();
    protected T Get<T>(string path)
        where T : Godot.Node => Root.GetNode<T>(path);
    public void Text(string path, string text) => Get<Label>(path).Text = text;
    protected void Wire(string path, Func<Task> action)
    {
        var button = Get<Button>(path);
        void Click() => App.Track(action());
        button.Pressed += Click;
    }

    protected void Listen(Action connect, Action disconnect)
    {
        connect();
        _disconnect.Add(disconnect);
    }

    protected void Feedback()
    {
        foreach (var button in Descendants(Root).OfType<Button>())
        {
            var original = button.SelfModulate;
            void Animate(Color color)
            {
                if (_feedback.Remove(button, out var previous))
                    previous.Kill();
                var tween = Root.CreateTween();
                tween.TweenProperty(button, "self_modulate", color, .12);
                _feedback[button] = tween;
            }

            void Enter() => Animate(original * new Color(1.02f, 1.02f, .92f));
            void Leave() => Animate(original);
            button.MouseEntered += Enter;
            button.MouseExited += Leave;
        }
    }

    public static IEnumerable<Godot.Node> Descendants(Godot.Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    protected override Task OnDisposeAsync()
    {
        foreach (var disconnect in _disconnect)
            disconnect();
        // Owned scenes release native signals and node-bound tweens when freed.
        _disconnect.Clear();
        _feedback.Clear();
        return Task.CompletedTask;
    }

    protected override void OnActivated(StateChangeContext context) => Refresh();
    public virtual void Refresh()
    {
    }

    protected Texture2D HeroTexture(int i) => GD.Load<Texture2D>($"res://Assets/Client/hero{i}.png");
    protected Texture2D ItemTexture(string id) => GD.Load<Texture2D>($"res://Assets/Items/{Catalog.Item(id).Icon}.svg");
}

[UIResource("res://Scenes/Client/Login.tscn")]
public class LoginView : ClientView
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Wire("LoginCard/Enter", App.EnterAsync);
        Feedback();
        return Task.CompletedTask;
    }

    public new void Text(string path, string text) => base.Text("LoginCard/" + path, text);
    public override void Refresh() => Text("Account", $"本地档案 · {Data.Name}");
}

[UIResource("res://Scenes/Client/Shell.tscn")]
public class ShellView : ClientView
{
    public StackContainerNode Pages { get; private set; } = null!;
    public LobbyView Home { get; private set; } = null!;

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Pages = await this.CreateContainerAsync<StackContainerNode>("Pages", Get<Control>("PageMount"), token);
        Pages.ChildTransition = GodotTransitions.SlideHorizontal(.24);
        foreach (var name in new[]
        {
            "Home",
            "Characters",
            "Inventory",
            "Team",
            "Stages",
            "Shop",
            "Tasks"
        })
            Wire("Bottom/" + name, () => App.OpenAsync(name));
        Wire("Top/Mail", () => App.OpenAsync("Mail"));
        Wire("Top/Back", App.BackAsync);
        Wire("Top/Credits", () => App.ModalAsync(new ModalArgs("Credits")));
        Wire("Top/Profile", () => App.ModalAsync(new ModalArgs("Profile")));
        Wire("Top/Settings", () => App.ModalAsync(new ModalArgs("Settings")));
        Listen(() => App.Store.Changed += Refresh, () => App.Store.Changed -= Refresh);
        Feedback();
        Refresh();
    }

    public async Task EnterLobbyAsync(CancellationToken token = default)
    {
        Home = await Pages.PushAsync<LobbyView>(cancellationToken: token);
    }

    public override void Refresh()
    {
        Get<Button>("Top/Profile").Text = Data.Name;
        Text("Top/Wallet", $"金币  {Data.Gold:N0}     星石  {Data.Gems}     徽章  {Data.Tokens}");
        Get<Button>("Top/Mail").Text = $"邮件 · {Enumerable.Range(0, 8).Count(i => !Data.ReadMail.Contains(i) && !Data.DeletedMail.Contains(i))}";
    }

    public void SetNavigation(bool enabled)
    {
        foreach (var b in Descendants(Get<Control>("Top")).Concat(Descendants(Get<Control>("Bottom"))).OfType<Button>())
            b.Disabled = !enabled;
    }
}

[UIResource("res://Scenes/Client/Lobby.tscn")]
public class LobbyView : ClientView
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

public abstract class ClientPage : ClientView
{
    public abstract string Module { get; }
    public TabContainerNode Tabs { get; private set; } = null!;

    private readonly Dictionary<string, int> _entries = new();
    private bool _favorites;
    private int _sort;
    public TextureRect SelectedIcon { get; private set; } = null!;

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", Get<Control>("Pages"), token);
        foreach (var page in Get<Control>("Pages").GetChildren().Cast<Control>())
        {
            var name = page.Name.ToString();
            await Tabs.BindAsync(name, new UINode { Transition = ClientTransitions.Category(Module) }, page, token);
            Wire("Tabs/" + name, () => SelectTabAsync(name));
            foreach (var button in page.GetChildren().OfType<Button>())
            {
                var path = $"Pages/{name}/{button.Name}";
                Wire(path, () => SelectEntryAsync(path));
            }
        }

        if (Module is "Inventory" or "Friends")
        {
            var filter = Get<LineEdit>("Filter");
            void Search(string text) => Refresh();
            filter.TextChanged += Search;
            Wire("FavoriteFilter", () =>
            {
                _favorites = !_favorites;
                Refresh();
                return Task.CompletedTask;
            });
        }

        Transition = ClientTransitions.Page(Module);
        if (Module == "Inventory")
        {
            var sort = Get<OptionButton>("Sort");
            foreach (var title in new[]
            {
                "按名称",
                "按属性",
                "按数量"
            })
                sort.AddItem(title);
            void Sort(long value)
            {
                _sort = (int)value;
                Refresh();
            }

            sort.ItemSelected += Sort;
        }

        if (Module == "Mail")
            Wire("ClaimAll", ClaimAllAsync);
        await Tabs.SwitchAsync(Get<Control>("Pages").GetChild<Control>(0).Name.ToString(), false, token);
        Feedback();
        Refresh();
    }

    public async Task SelectTabAsync(string name)
    {
        if (Tabs.CurrentName == name)
            return;
        await App.RunAsync(this, async () =>
        {
            await Tabs.SwitchAsync(name);
            Refresh();
        });
    }

    public override void Refresh()
    {
        if (Tabs == null)
            return;
        foreach (var page in Get<Control>("Pages").GetChildren().Cast<Control>())
        {
            var name = page.Name.ToString();
            var ids = Entries(name).ToArray();
            var buttons = page.GetChildren().OfType<Button>().ToArray();
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                b.Visible = i < ids.Length;
                if (i >= ids.Length)
                    continue;
                int id = ids[i];
                string path = $"Pages/{name}/{b.Name}";
                _entries[path] = id;
                var(title, info, texture) = Describe(name, id);
                b.GetNode<Label>("Title").Text = title;
                b.GetNode<Label>("Info").Text = info;
                b.GetNode<TextureRect>("Icon").Texture = texture;
            }
        }

        if (Module == "Mail")
            Get<Button>("ClaimAll").Disabled = !Enumerable.Range(0, 8).Any(i => !Data.DeletedMail.Contains(i) && !Data.Claimed.Contains($"m{i}"));
    }

    private IEnumerable<int> Entries(string tab)
    {
        var ids = Module switch
        {
            "Characters" => Enumerable.Range(0, 6),
            "Team" => Enumerable.Range(0, 4),
            "Tasks" or "Achievements" => Enumerable.Range(0, 12),
            "Mail" => Enumerable.Range(0, 8).Where(i => !Data.DeletedMail.Contains(i)),
            "Friends" => Data.Friends.OrderBy(i => i),
            "Stages" => Enumerable.Range(tab == "剧情" ? 0 : 6, tab == "剧情" ? 6 : 4),
            "Inventory" or "Shop" => Enumerable.Range(tab == "装备" ? 0 : tab == "材料" ? 12 : 0, tab == "装备" ? 12 : tab == "材料" ? 8 : 3),
            _ => throw new InvalidOperationException(Module)};
        if (Module == "Inventory")
        {
            var query = Get<LineEdit>("Filter").Text;
            ids = ids.Where(i => Catalog.Items[i].Name.Contains(query) && (!_favorites || Data.Favorites.Contains(Catalog.Items[i].Id)));
            ids = _sort switch
            {
                1 => ids.OrderByDescending(i => Catalog.Items[i].Power),
                2 => ids.OrderByDescending(i => Data.Inventory[Catalog.Items[i].Id]),
                _ => ids.OrderBy(i => Catalog.Items[i].Name, StringComparer.Ordinal)};
        }

        if (Module == "Friends")
        {
            var query = Get<LineEdit>("Filter").Text;
            ids = ids.Where(i => Catalog.Friends[i].Contains(query) && (!_favorites || Data.FavoriteFriends.Contains(i)));
        }

        return ids;
    }

    private (string, string, Texture2D) Describe(string tab, int id)
    {
        if (Module == "Characters")
            return (Catalog.Heroes[id].Name, $"{Catalog.Heroes[id].Role}\nLv.{Data.Levels[id]} · 战力 {Data.Power(id)}", HeroTexture(id));
        if (Module == "Team")
        {
            int hero = Data.Team[id];
            return ($"位置 {id + 1}", hero < 0 ? "选择一位同行者" : $"{Catalog.Heroes[hero].Name}\n战力 {Data.Power(hero)}", hero < 0 ? ItemTexture("eq5") : HeroTexture(hero));
        }

        if (Module is "Inventory" or "Shop")
        {
            var item = Catalog.Items[id];
            return (item.Name, Module == "Shop" ? $"{(tab == "礼包" ? "补给礼包 · " : "")}{item.Price} 金币\n查看与购买" : $"持有 {Data.Inventory[item.Id]}\n{(item.Power > 0 ? $"属性 +{item.Power}" : "培养材料")}{(Data.Favorites.Contains(item.Id) ? " · 已收藏" : "")}", ItemTexture(item.Id));
        }

        if (Module is "Tasks" or "Achievements")
        {
            bool a = Module == "Achievements";
            var g = (a ? Catalog.Achievements : Catalog.Tasks)[id];
            bool claimed = Data.Claimed.Contains($"{(a ? "a" : "t")}{id}");
            return (g.Name, $"进度 {Math.Min(Data.Counts[g.Action], g.Target)}/{g.Target}\n{(claimed ? "已领取" : App.Store.CanClaim(a, id) ? "可领取 · 200 金币" : "查看目标")}", ItemTexture(Catalog.Items[id].Id));
        }

        if (Module == "Stages")
            return (Catalog.Stages[id], id < 6 ? $"第 {id + 1} 章 · 查看目的地" : "挑战详情 · 配置队伍", ItemTexture(Catalog.Items[id].Id));
        if (Module == "Mail")
            return (Catalog.MailTitles[id], $"{(Data.ReadMail.Contains(id) ? "已读" : "未读")} · {(Data.Claimed.Contains($"m{id}") ? "附件已领取" : "300 金币 / 手记 × 2")}", ItemTexture("eq5"));
        return (Catalog.Friends[id], $"旅行者 Lv.{15 + id * 3}\n{(Data.FavoriteFriends.Contains(id) ? "已收藏" : "查看名片")}", HeroTexture(id));
    }

    private async Task SelectEntryAsync(string path)
    {
        int id = _entries[path];
        if (Module == "Characters")
        {
            await App.RunAsync(() => App.Shell.Pages.PushAsync<HeroDetailsView>(id));
            return;
        }

        if (Module == "Inventory")
        {
            SelectedIcon = Get<TextureRect>(path + "/Icon");
            await App.RunAsync(() => App.Shell.Pages.PushAsync<ItemDetailsView>(Catalog.Items[id].Id));
            return;
        }

        if (Module == "Shop")
        {
            await App.ModalAsync(new ModalArgs("Shop", id, Text: Tabs.CurrentName == "礼包" ? "礼包包含当前装备 × 1，以及经验手记 × 3。" : ""));
            return;
        }

        if (Module == "Team")
        {
            await App.ModalAsync(new ModalArgs("Team", id));
            return;
        }

        if (Module is "Tasks" or "Achievements")
        {
            await App.ModalAsync(new ModalArgs(Module, id));
            return;
        }

        if (Module == "Stages")
        {
            App.Store.Count("stage");
            await App.ModalAsync(new ModalArgs("Stage", id));
            return;
        }

        if (Module == "Mail")
        {
            App.Store.Change(s => s.ReadMail.Add(id));
            await App.ModalAsync(new ModalArgs("Mail", id));
            return;
        }

        await App.ModalAsync(new ModalArgs("Friend", id));
    }

    private async Task ClaimAllAsync()
    {
        var count = Enumerable.Range(0, 8).Count(i => !Data.DeletedMail.Contains(i) && !Data.Claimed.Contains($"m{i}"));
        App.Store.ClaimMail(Enumerable.Range(0, 8));
        Refresh();
        await App.RewardAsync("所有邮件附件已收下", new("金币", "compass", 300 * count), new("经验手记", Catalog.Item("mat6").Icon, 2 * count));
    }
}

[UIResource("res://Scenes/Client/Characters.tscn")]
public class CharactersView : ClientPage
{
    public override string Module => "Characters";
}

[UIResource("res://Scenes/Client/Inventory.tscn")]
public class InventoryView : ClientPage
{
    public override string Module => "Inventory";
}

[UIResource("res://Scenes/Client/Team.tscn")]
public class TeamView : ClientPage
{
    public override string Module => "Team";
}

[UIResource("res://Scenes/Client/Stages.tscn")]
public class StagesView : ClientPage
{
    public override string Module => "Stages";
}

[UIResource("res://Scenes/Client/Shop.tscn")]
public class ShopView : ClientPage
{
    public override string Module => "Shop";
}

[UIResource("res://Scenes/Client/Tasks.tscn")]
public class TasksView : ClientPage
{
    public override string Module => "Tasks";
}

[UIResource("res://Scenes/Client/Achievements.tscn")]
public class AchievementsView : ClientPage
{
    public override string Module => "Achievements";
}

[UIResource("res://Scenes/Client/Mail.tscn")]
public class MailView : ClientPage
{
    public override string Module => "Mail";
}

[UIResource("res://Scenes/Client/Friends.tscn")]
public class FriendsView : ClientPage
{
    public override string Module => "Friends";
}

[UIResource("res://Scenes/Client/HeroDetails.tscn")]
public class HeroDetailsView : UINode<int>
{
    private DemoApp App => DemoApp.Instance;
    private Control Root => ((GodotUIElement)Element!).Control;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Hero();
        Root.GetNode<Button>("Train").Pressed += Train;
        Root.GetNode<Button>("Equip").Pressed += Equip;
        Root.GetNode<Button>("Unequip").Pressed += Unequip;
        Refresh();
        return Task.CompletedTask;
    }

    private void Train() => App.Track(TrainAsync());
    public async Task TrainAsync()
    {
        App.Store.Train(Parameters);
        Refresh();
        await App.Modals.OpenAsync<TrainingView>(new PresentationArgs($"{Catalog.Heroes[Parameters].Name}\n等级提升至 Lv.{App.Store.State.Levels[Parameters]}\n战力 {App.Store.State.Power(Parameters)}", Parameters));
    }

    private void Equip() => App.Track(App.ModalAsync(new ModalArgs("Equip", Parameters)));
    private void Unequip()
    {
        App.Store.Equip(Parameters, "");
        Refresh();
    }

    protected override void OnActivated(StateChangeContext context) => Refresh();
    public void Refresh()
    {
        var s = App.Store.State;
        var hero = Catalog.Heroes[Parameters];
        Root.GetNode<TextureRect>("Portrait").Texture = GD.Load<Texture2D>($"res://Assets/Client/hero{Parameters}.png");
        Root.GetNode<Label>("Name").Text = hero.Name + " · " + hero.Role;
        Root.GetNode<Label>("Story").Text = hero.Story;
        Root.GetNode<Label>("Stats").Text = $"Lv.{s.Levels[Parameters]}       战力 {s.Power(Parameters)}";
        Root.GetNode<Label>("Equipment").Text = "当前装备：" + (s.Equipment[Parameters] == "" ? "未装备" : Catalog.Item(s.Equipment[Parameters]).Name);
        Root.GetNode<Button>("Unequip").Disabled = s.Equipment[Parameters] == "";
        Root.GetNode<Button>("Train").Disabled = s.Gold < 100 || s.Inventory["mat6"] < 1 || s.Levels[Parameters] >= 60;
    }

}

[UIResource("res://Scenes/Client/ItemDetails.tscn")]
public class ItemDetailsView : UINode<string>
{
    private DemoApp App => DemoApp.Instance;
    private Control Root => ((GodotUIElement)Element!).Control;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = Transitions.Select(c => c.Source is InventoryView || c.Target is InventoryView ? new ItemPartnerTransition() : GodotTransitions.SlideHorizontal(.24));
        Root.GetNode<Button>("Favorite").Pressed += Favorite;
        Root.GetNode<Button>("Compare").Pressed += Compare;
        Refresh();
        return Task.CompletedTask;
    }

    private void Favorite()
    {
        App.Store.Favorite(Parameters);
        Refresh();
    }

    private void Compare() => App.Track(CompareAsync());
    public async Task CompareAsync()
    {
        if (App.Floating.Children.Count >= 2)
            throw new InvalidOperationException("对比最多两件装备。");
        if (App.Floating.Children.Count == 0)
            await App.Floating.OpenAsync<CompareView>(new CompareArgs(Parameters, 0));
        else
            await App.ModalAsync(new ModalArgs("Compare"));
        Refresh();
    }

    protected override void OnActivated(StateChangeContext context) => Refresh();
    public void Refresh()
    {
        var item = Catalog.Item(Parameters);
        Root.GetNode<TextureRect>("Icon").Texture = GD.Load<Texture2D>($"res://Assets/Items/{item.Icon}.svg");
        Root.GetNode<Label>("Name").Text = item.Name;
        Root.GetNode<Label>("Description").Text = item.Description;
        Root.GetNode<Label>("Count").Text = $"持有 {App.Store.State.Inventory[Parameters]}  ·  {(item.Power > 0 ? $"属性 +{item.Power}" : "培养材料")}";
        Root.GetNode<Button>("Favorite").Text = App.Store.State.Favorites.Contains(Parameters) ? "取消收藏" : "收藏";
        Root.GetNode<Button>("Compare").Disabled = item.Power == 0 || App.Floating.Children.Count >= 2;
    }

}

[UIResource("res://Scenes/Client/Events.tscn")]
public class EventsView : ClientView
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
