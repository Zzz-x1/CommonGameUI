using Godot;
using UIFramework;
using UIFramework.Godot;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Godot;

namespace HierarchyDemo;
public sealed record ModalArgs(string Kind, int Id = 0, int Quantity = 1, string Text = "", UINode? ReturnTo = null);
public sealed record RewardEntry(string Name, string Icon, int Count);
public sealed record PresentationArgs(string Text, int Hero = 0, RewardEntry[]? Rewards = null);
public sealed record CompareArgs(string Item, int Position);
[UIResource("res://Scenes/Client/Modal.tscn")]
public class ClientModalView : UINode<ModalArgs>
{
    private DemoApp App => DemoApp.Instance;
    private PlayerState Data => App.Store.State;
    private Control Root => ((GodotUIElement)Element!).Control;

    private int _avatar;
    private TabContainerNode? _settings;
    private TabContainerNode _pages = null!;
    private T Get<T>(string path)
        where T : Godot.Node => Root.GetNode<T>(path);
    private void Text(string path, string text) => Get<Label>(path).Text = text;
    private void Wire(string path, Func<Task> action)
    {
        var b = Get<Button>(path);
        void Click() => App.Track(action());
        b.Pressed += Click;
    }

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Popup(Parameters.Kind);
        DismissRequested = _ => App.Modals.CloseAsync(this);
        Wire("Center/Close", () => App.Modals.CloseAsync(this));
        Wire("Center/Cancel", CancelAsync);
        if (Parameters.Kind == "Purchase")
        {
            var purchase = this.CreateViewModel<ClientPurchaseVm>("Purchase"); purchase.Initialize(App.Store, Parameters);
            this.BindCommand("Purchase.Buy", Get<Button>("Center/Confirm").ClickTarget());
        }
        Wire("Center/Confirm", ConfirmAsync);
        _pages = await this.CreateContainerAsync<TabContainerNode>("Content", Get<Control>("Center/Pages"), token);
        foreach (var c in Get<Control>("Center/Pages").GetChildren().Cast<Control>())
            await _pages.BindAsync(c.Name.ToString(), c, token);
        string page = Parameters.Kind switch
        {
            "Shop" => "Shop",
            "Team" or "Equip" or "Compare" => "Pick",
            "Settings" => "Settings",
            "Profile" => "Profile",
            _ => "Text"
        };
        await _pages.SwitchAsync(page, false, token);
        if (page == "Pick")
            for (int i = 0; i < 12; i++)
            {
                int id = i;
                Wire($"Center/Pages/Pick/C{i}", () => PickAsync(id));
            }

        if (page == "Shop")
        {
            var spin = Get<SpinBox>("Center/Pages/Shop/Quantity");
            spin.Value = Parameters.Quantity;
            void Changed(double value) => Refresh();
            spin.ValueChanged += Changed;
        }

        if (page == "Profile")
        {
            _avatar = Data.Avatar;
            Get<LineEdit>("Center/Pages/Profile/Name").Text = Data.Name;
            for (int i = 0; i < 6; i++)
            {
                int id = i;
                Wire($"Center/Pages/Profile/C{i}", () =>
                {
                    _avatar = id;
                    Refresh();
                    return Task.CompletedTask;
                });
            }

            var name = Get<LineEdit>("Center/Pages/Profile/Name");
            void Changed(string value) => Refresh();
            name.TextChanged += Changed;
        }

        if (page == "Settings")
            await InitializeSettingsAsync(token);
        Refresh();
    }

    private async Task InitializeSettingsAsync(CancellationToken token)
    {
        string path = "Center/Pages/Settings/";
        _settings = await this.CreateContainerAsync<TabContainerNode>("Preferences", Get<Control>(path + "SettingPages"), token);
        string[] names =
        {
            "声音",
            "画面",
            "演出"
        };
        for (int i = 0; i < 3; i++)
        {
            var n = names[i];
            await _settings.BindAsync(n, new UINode { Transition = ClientTransitions.Category("Settings") }, Get<Control>(path + "SettingPages/" + n), token);
            Wire(path + "Tab" + i, () => _settings.CurrentName == n ? Task.CompletedTask : App.RunAsync(this, () => _settings.SwitchAsync(n)));
        }

        await _settings.SwitchAsync("声音", false, token);
        var vm = this.CreateViewModel<ClientSettingsVm>("Settings");
        vm.Initialize(App.Store, App.ApplySettings);
        this.Bind("Settings.Volume", Get<HSlider>(path + "SettingPages/声音/Volume").ValueTarget(), BindingMode.TwoWay);
        this.Bind("Settings.Fullscreen", Get<CheckButton>(path + "SettingPages/画面/Fullscreen").ToggleTarget(), BindingMode.TwoWay);
        this.Bind("Settings.FullMotion", Get<CheckButton>(path + "SettingPages/演出/FullMotion").ToggleTarget(), BindingMode.TwoWay);
        var quality = Get<OptionButton>(path + "SettingPages/画面/Quality");
        foreach (var n in new[]
        {
            "流畅 · 偏好模拟",
            "均衡 · 偏好模拟",
            "精致 · 偏好模拟"
        })
            quality.AddItem(n);
        quality.Select(Data.Quality);
        void Quality(long v) => App.Store.Change(s => s.Quality = (int)v);
        quality.ItemSelected += Quality;
    }

    protected override void OnActivated(StateChangeContext context) => Refresh();
    protected override void OnEntered(LifecycleContext context)
    {
        var button = Get<Button>("Center/Confirm");
        if (button.Disabled)
            Get<Button>("Center/Close").GrabFocus();
        else
            button.GrabFocus();
    }

    public void Refresh()
    {
        var p = Parameters;
        var yes = Get<Button>("Center/Confirm");
        yes.Disabled = false;
        yes.Text = "确定";
        Text("Center/Description", p.Text);
        Get<Button>("Center/Cancel").Text = "返回";
        switch (p.Kind)
        {
            case "Shop":
                var item = Catalog.Items[p.Id];
                int quantity = (int)Get<SpinBox>("Center/Pages/Shop/Quantity").Value;
                Text("Center/Title", "商品详情");
                Get<TextureRect>("Center/Pages/Shop/Icon").Texture = GD.Load<Texture2D>($"res://Assets/Items/{item.Icon}.svg");
                Text("Center/Pages/Shop/Name", item.Name);
                Text("Center/Pages/Shop/Price", $"单价 {item.Price} 金币\n总计 {quantity * item.Price} 金币");
                yes.Text = Data.Gold < quantity * item.Price ? "金币不足" : "确认订单";
                yes.Disabled = Data.Gold < quantity * item.Price;
                break;
            case "Purchase":
                Text("Center/Title", "确认购买");
                Text("Center/Pages/Text/Body", $"{Catalog.Items[p.Id].Name} × {p.Quantity}\n\n共计 {Catalog.Items[p.Id].Price * p.Quantity} 金币\n\n购买后返回商品页面，奖励自动存入背包。");
                yes.Text = "购买";
                yes.Disabled = Data.Gold < Catalog.Items[p.Id].Price * p.Quantity;
                break;
            case "Team":
            case "Equip":
            case "Compare":
                bool team = p.Kind == "Team";
                Text("Center/Title", team ? $"配置队伍位置 {p.Id + 1}" : "选择装备");
                for (int i = 0; i < 12; i++)
                {
                    var b = Get<Button>($"Center/Pages/Pick/C{i}");
                    b.Visible = !team || i < 6;
                    if (team && i >= 6)
                        continue;
                    b.GetNode<Label>("Title").Text = team ? Catalog.Heroes[i].Name : Catalog.Items[i].Name;
                    b.GetNode<TextureRect>("Icon").Texture = team ? GD.Load<Texture2D>($"res://Assets/Client/hero{i}.png") : GD.Load<Texture2D>($"res://Assets/Items/{Catalog.Items[i].Icon}.svg");
                    bool equipped = !team && p.Kind != "Compare" && Data.Equipment[p.Id] == Catalog.Items[i].Id;
                    b.Disabled = team ? Data.Team.Contains(i) : p.Kind == "Compare" ? Data.Inventory[Catalog.Items[i].Id] < 1 : equipped || Data.Inventory[Catalog.Items[i].Id] <= Data.Equipment.Count(x => x == Catalog.Items[i].Id);
                    b.GetNode<Label>("Info").Text = team ? (b.Disabled ? "已经入队" : $"Lv.{Data.Levels[i]}") : (equipped ? "当前装备" : b.Disabled ? "全部使用中" : $"属性 +{Catalog.Items[i].Power}");
                }

                yes.Text = team ? "清空此位置" : "关闭";
                yes.Disabled = team && Data.Team[p.Id] < 0;
                break;
            case "Tasks":
            case "Achievements":
                bool achievement = p.Kind == "Achievements";
                var goal = (achievement ? Catalog.Achievements : Catalog.Tasks)[p.Id];
                Text("Center/Title", goal.Name);
                Text("Center/Pages/Text/Body", $"目标：{ActionText(goal.Action)} {goal.Target} 次\n进度：{Math.Min(Data.Counts[goal.Action], goal.Target)}/{goal.Target}\n\n奖励：金币 × 200\n经验手记 × 2");
                yes.Disabled = !App.Store.CanClaim(achievement, p.Id);
                yes.Text = Data.Claimed.Contains($"{(achievement ? "a" : "t")}{p.Id}") ? "已领取" : "领取奖励";
                break;
            case "Mail":
                Text("Center/Title", Catalog.MailTitles[p.Id]);
                Text("Center/Pages/Text/Body", "亲爱的旅行者：\n\n愿星光照亮你的行囊。这里有一份来自星庭的补给，愿你与同行者度过美好的一天。\n\n附件：金币 × 300 / 经验手记 × 2\n\n—— 星庭事务所");
                yes.Text = Data.Claimed.Contains($"m{p.Id}") ? "删除邮件" : "领取附件";
                break;
            case "Friend":
                Text("Center/Title", Catalog.Friends[p.Id]);
                Text("Center/Pages/Text/Body", $"旅行者编号 100{p.Id + 1}\n等级 {15 + p.Id * 3}\n签名：在下一阵风里，遇见新的故事。\n\n这是本地模拟联系人，没有在线或聊天服务。");
                yes.Text = Data.FavoriteFriends.Contains(p.Id) ? "取消收藏" : "收藏同行者";
                Get<Button>("Center/Cancel").Text = "移除好友";
                break;
            case "Stage":
                Text("Center/Title", Catalog.Stages[p.Id]);
                Text("Center/Pages/Text/Body", $"目的地：{Catalog.Stages[p.Id]}\n推荐战力：{300 + p.Id * 80}\n当前队伍：{string.Join("、", Data.Team.Where(i => i >= 0).Select(i => Catalog.Heroes[i].Name))}\n\n确认后仅完成出战 UI 流程。\n不进入战斗，不消耗体力，不产生战斗奖励。");
                yes.Disabled = Data.Team.All(i => i < 0);
                yes.Text = "确认出发";
                break;
            case "Departure":
                Text("Center/Title", "旅程已准备就绪");
                Text("Center/Pages/Text/Body", "队伍与目的地已确认。\n\n纯 UI 演示在这里结束，继续可返回远征地图。\n没有扣除资源或发放战斗奖励。");
                yes.Text = "返回地图";
                break;
            case "Profile":
                Text("Center/Title", "旅行者名片");
                Text("Center/Description", $"玩家编号 10001 · 购买 {Data.Counts["purchase"]} 次 · 培养 {Data.Counts["train"]} 次");
                for (int i = 0; i < 6; i++)
                {
                    var b = Get<Button>($"Center/Pages/Profile/C{i}");
                    b.GetNode<Label>("Title").Text = Catalog.Heroes[i].Name;
                    b.GetNode<Label>("Info").Text = i == _avatar ? "已选头像 / 看板" : "选择角色";
                }

                yes.Disabled = string.IsNullOrWhiteSpace(Get<LineEdit>("Center/Pages/Profile/Name").Text);
                yes.Text = "保存名片";
                break;
            case "Settings":
                Text("Center/Title", "客户端设置");
                Text("Center/Description", "偏好自动保存 · 画质选项为模拟展示");
                yes.Text = "恢复默认";
                break;
            case "Credits":
                Text("Center/Title", "制作与素材");
                Text("Center/Pages/Text/Body", "星庭远征 · 原创纯 UI Demo\n\n角色与背景：OpenAI imagegen 原创生成\n图标：Lorc / Delapouite · game-icons.net · CC BY 3.0\n字体：Noto Sans CJK · SIL OFL 1.1\n\n完整来源与生成提示见 Assets/SOURCES.md。");
                yes.Text = "关闭";
                break;
            default:
                throw new ArgumentException(p.Kind);
        }
    }

    private static string ActionText(string action) => action switch
    {
        "login" => "进入客户端",
        "purchase" => "购买商品",
        "equip" => "更换装备",
        "train" => "培养角色",
        "team" => "调整编队",
        "mail" => "领取邮件",
        "favorite" => "收藏物品",
        "profile" => "保存名片",
        "sign" => "星光签到",
        "stage" => "查看目的地",
        "activity" => "领取活动奖励",
        "friend" => "收藏好友",
        _ => throw new ArgumentException(action)};
    public async Task PickAsync(int id)
    {
        if (Parameters.Kind == "Team")
            App.Store.SetTeam(Parameters.Id, id);
        else if (Parameters.Kind == "Compare")
            await App.Floating.OpenAsync<CompareView>(new CompareArgs(Catalog.Items[id].Id, 1));
        else
            App.Store.Equip(Parameters.Id, Catalog.Items[id].Id);
        await App.Modals.CloseAsync(this);
    }

    public async Task CancelAsync()
    {
        if (Parameters.Kind == "Friend")
        {
            if (!await App.AskAsync("从同行名册移除这位朋友？"))
                return;
            App.Store.Change(s =>
            {
                s.Friends.Remove(Parameters.Id);
                s.FavoriteFriends.Remove(Parameters.Id);
            });
        }

        await App.Modals.CloseAsync(this);
    }

    public async Task ConfirmAsync()
    {
        var p = Parameters;
        switch (p.Kind)
        {
            case "Shop":
                await App.ModalAsync(new ModalArgs("Purchase", p.Id, (int)Get<SpinBox>("Center/Pages/Shop/Quantity").Value, p.Text, this));
                return;
            case "Purchase":
                await App.Modals.CloseToAsync(p.ReturnTo!);
                var rewards = new List<RewardEntry>
                {
                    new(Catalog.Items[p.Id].Name, Catalog.Items[p.Id].Icon, p.Quantity)
                };
                if (p.Text != "")
                    rewards.Add(new("经验手记", Catalog.Item("mat6").Icon, 3 * p.Quantity));
                await App.RewardAsync("所购物品已存入背包", rewards.ToArray());
                return;
            case "Team":
                App.Store.SetTeam(p.Id, -1);
                break;
            case "Tasks":
            case "Achievements":
                App.Store.ClaimGoal(p.Kind == "Achievements", p.Id);
                await App.Modals.CloseAsync(this);
                await App.RewardAsync("目标奖励已领取", new("金币", "compass", 200), new("经验手记", Catalog.Item("mat6").Icon, 2));
                return;
            case "Mail":
                if (Data.Claimed.Contains($"m{p.Id}"))
                {
                    if (!await App.AskAsync("删除这封已领取邮件？"))
                        return;
                    App.Store.Change(s => s.DeletedMail.Add(p.Id));
                }
                else
                {
                    App.Store.ClaimMail(new[] { p.Id });
                    await App.RewardAsync("邮件附件已领取", new("金币", "compass", 300), new("经验手记", Catalog.Item("mat6").Icon, 2));
                    return;
                }

                break;
            case "Friend":
                App.Store.Change(s =>
                {
                    if (!s.FavoriteFriends.Add(p.Id))
                        s.FavoriteFriends.Remove(p.Id);
                    else
                        s.Counts["friend"]++;
                });
                Refresh();
                return;
            case "Stage":
                await App.Modals.CloseAsync(this);
                await App.ModalAsync(new ModalArgs("Departure"));
                return;
            case "Profile":
                App.Store.Change(s =>
                {
                    s.Name = Get<LineEdit>("Center/Pages/Profile/Name").Text.Trim();
                    s.Avatar = _avatar;
                    s.Showcase = _avatar;
                    s.Counts["profile"]++;
                });
                break;
            case "Settings":
                if (!await App.AskAsync("恢复客户端默认设置？"))
                    return;
                App.Store.Change(s =>
                {
                    s.Volume = 70;
                    s.FullMotion = true;
                    s.Fullscreen = false;
                    s.Quality = 2;
                });
                App.ApplySettings();
                await App.Modals.CloseAsync(this);
                await App.ModalAsync(new ModalArgs("Settings"));
                return;
        }

        await App.Modals.CloseAsync(this);
    }

}

public class ConfirmView(string message) : DialogNode<bool>
{
    private Control Root => ((GodotUIElement)Element!).Control;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Popup("Confirm");
        foreach (var page in Root.GetNode<Control>("Center/Pages").GetChildren().Cast<Control>())
            page.Visible = page.Name == "Text";
        Root.GetNode<Label>("Center/Title").Text = "请确认";
        Root.GetNode<Label>("Center/Pages/Text/Body").Text = message;
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

[UIResource("res://Scenes/Client/Compare.tscn", WindowMode = WindowInstanceMode.Multiple)]
public class CompareView : UINode<CompareArgs>
{
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        Transition = ClientTransitions.Comparison(Parameters.Position);
        var root = this.GetControl();
        var card = root.GetNode<Control>("Card");
        card.AnchorLeft += Parameters.Position * .33f;
        card.AnchorRight += Parameters.Position * .33f;
        var item = Catalog.Item(Parameters.Item);
        root.GetNode<TextureRect>("Card/Icon").Texture = GD.Load<Texture2D>($"res://Assets/Items/{item.Icon}.svg");
        root.GetNode<Label>("Card/Info").Text = $"{item.Name}\n属性 +{item.Power}\n持有 {DemoApp.Instance.Store.State.Inventory[item.Id]}";
        root.GetNode<Button>("Card/Close").Pressed += Close;
        return Task.CompletedTask;
    }

    private void Close() => DemoApp.Instance.Track(CloseAsync());
    private async Task CloseAsync()
    {
        await DemoApp.Instance.Floating.CloseAsync(this);
        if (DemoApp.Instance.Shell.Pages.Current is ItemDetailsView item)
            item.Refresh();
    }

}

public abstract class PresentationView : UINode<PresentationArgs>
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

[UIResource("res://Scenes/Client/Reward.tscn")]
public class RewardView : PresentationView
{
}

[UIResource("res://Scenes/Client/Training.tscn")]
public class TrainingView : PresentationView
{
}

/// <summary>Project timeline orchestration. AnimationPlayer tracks contain presentation only.</summary>
public sealed class CinematicTransition : IUITransition
{
    public IUITransitionRun Create(TransitionContext context)
    {
        var root = ((GodotUIElement)context.Element!).Control;
        var player = root.GetNode<AnimationPlayer>("AnimationPlayer");
        var originals = new Dictionary<(Godot.Node, string), Variant>();
        foreach (var clip in new[]
        {
            "enter",
            "exit"
        })
        {
            var animation = player.GetAnimation(clip);
            for (int i = 0; i < animation.GetTrackCount(); i++)
            {
                var path = animation.TrackGetPath(i).ToString().Split(':', 2);
                var node = root.GetNode(path[0]);
                var key = (node, path[1]);
                originals.TryAdd(key, node.Get(path[1]));
            }
        }

        var scaled = originals.Keys.Where(k => k.Item2 == "scale").Select(k => (Control)k.Item1).ToArray();
        foreach (var control in scaled)
            originals.TryAdd((control, "pivot_offset"), control.PivotOffset);
        player.SpeedScale = DemoApp.Instance.Store.State.FullMotion ? 1 : 5;
        CpuParticles2D? particles = null;
        if (context.Node is PresentationView)
        {
            particles = root.GetNode<CpuParticles2D>("Center/Particles");
        }

        var inner = GodotTransitions.Animation("AnimationPlayer", "enter", "exit", _ =>
        {
            foreach (var pair in originals)
                pair.Key.Item1.Set(pair.Key.Item2, pair.Value);
        }).Create(context);
        return new Run(inner, root, player, context.Entering, particles, scaled, (GodotUIHost)context.Node.Context.Host);
    }

    private sealed class Run(IUITransitionRun inner, Control root, AnimationPlayer player, bool entering, CpuParticles2D? particles, Control[] scaled, GodotUIHost host) : IUITransitionRun
    {
        private readonly Color _color = root.Modulate;
        private CancellationToken _token;
        public async Task PlayAsync(CancellationToken token)
        {
            _token = token;
            token.ThrowIfCancellationRequested();
            // Keep layout active, but do not draw the authored end state while it settles.
            if (entering)
                root.Modulate = new Color(_color.R, _color.G, _color.B, 0);
            await host.NextFrameAsync();
            token.ThrowIfCancellationRequested();
            foreach (var control in scaled)
                control.PivotOffset = control.Size / 2;
            if (particles != null)
            {
                particles.Position = particles.GetParent<Control>().Size / 2;
                particles.Emitting = entering && DemoApp.Instance.Store.State.FullMotion;
            }
            if (entering)
                DemoApp.Instance.Gate(() => player.Advance(player.GetAnimation("enter").Length - player.CurrentAnimationPosition + .01));
            try
            {
                root.Modulate = _color;
                await inner.PlayAsync(token);
            }
            finally
            {
                if (entering && GodotObject.IsInstanceValid(DemoApp.Instance))
                    DemoApp.Instance.Gate(null);
            }
        }

        public void Restore()
        {
            try
            {
                if (!_token.IsCancellationRequested || GodotObject.IsInstanceValid(root)) root.Modulate = _color;
                if (particles != null && (!_token.IsCancellationRequested || GodotObject.IsInstanceValid(particles))) particles.Emitting = false;
            }
            finally { inner.Restore(); }
        }
    }
}
