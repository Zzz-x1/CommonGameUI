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
public abstract partial class ClientPage : ClientView
{
    public abstract string Module { get; }
    public TabContainerNode Tabs { get; private set; } = null!;

    private readonly Dictionary<string, int> _entries = new();
    private bool _favorites;
    private int _sort;
    private ClientInventoryVm? _inventoryVm;
    private ClientShopVm? _shopVm;
    public TextureRect SelectedIcon { get; private set; } = null!;

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Tabs = await this.CreateContainerAsync<TabContainerNode>("Categories", Get<Control>("Pages"), token);
        foreach (var page in Get<Control>("Pages").GetChildren().Cast<Control>())
        {
            var name = page.Name.ToString();
            await Tabs.BindAsync(name, new UINode { Transition = ClientTransitions.Category(Module) }, page, token);
            Wire("Tabs/" + name, () => SelectTabAsync(name));
            foreach (var button in page.GetChildren().OfType<Button>().Where(_ => Module is not "Inventory" and not "Shop"))
            {
                var path = $"Pages/{name}/{button.Name}";
                Wire(path, () => SelectEntryAsync(path));
            }
        }

        if (Module == "Inventory")
        {
            _inventoryVm = this.CreateViewModel<ClientInventoryVm>("Inventory");
            var row = GD.Load<PackedScene>("res://Scenes/Client/InventoryRow.tscn");
            foreach (var name in new[] { "装备", "材料" })
            {
                var mount = Get<Control>("Pages/" + name);
                foreach (var child in mount.GetChildren()) child.Free();
                this.BindCollection(name == "装备" ? "Inventory.Equipment" : "Inventory.Materials", new InventoryGridTarget(this, mount, row));
            }
        }
        if (Module == "Shop")
        {
            _shopVm = this.CreateViewModel<ClientShopVm>("Shop"); _shopVm.Open = id => App.ModalAsync(new ModalArgs("Shop", id, Text: Tabs.CurrentName == "礼包" ? "礼包包含当前装备 × 1，以及经验手记 × 3。" : ""));
            this.SetViewModel("Player", App.PlayerVm);
            foreach (var name in new[] { "装备", "材料", "礼包" })
            {
                var mount = Get<Control>("Pages/" + name); foreach (var child in mount.GetChildren()) child.Free();
                var target = new EngineMvvm.CollectionTarget(this, mount, null, factory: new ShopItemFactory(), sources: new[] {
                    new CollectionSourceMapping { ParentSource = "Shop", ItemSource = "Shop" }, new CollectionSourceMapping { ParentSource = "Player", ItemSource = "Player" } });
                target.Changed += () => ArrangeShop(mount);
                this.BindCollection("Shop." + (name == "装备" ? "Equipment" : name == "材料" ? "Materials" : "Bundles"), target);
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
        if (Module == "Inventory")
        {
            UpdateInventory("装备", _inventoryVm!.Equipment); UpdateInventory("材料", _inventoryVm.Materials); return;
        }
        if (Module == "Shop")
        {
            foreach (var name in new[] { "装备", "材料", "礼包" })
            {
                var items = name == "装备" ? _shopVm!.Equipment : name == "材料" ? _shopVm!.Materials : _shopVm!.Bundles;
                if (items.Count == 0) foreach (var id in Entries(name)) { var (title, info, icon) = Describe(name, id); items.Add(new ClientInventoryItemVm { Id = id, Title = title, Info = info, Icon = icon }); }
            }
            return;
        }
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

    private static void ArrangeShop(Control mount)
    {
        var rows = mount.GetChildren().OfType<Control>().ToArray();
        for (int i = 0; i < rows.Length; i++) { var row = rows[i]; row.Name = "C" + i; row.AnchorLeft = .006f + .25f * (i % 4); row.AnchorRight = row.AnchorLeft + .238f; row.AnchorTop = .012f + i / 4 / 3f; row.AnchorBottom = row.AnchorTop + .3093333f; row.OffsetLeft = row.OffsetRight = row.OffsetTop = row.OffsetBottom = 0; }
    }
    private sealed class ShopItemFactory : ICollectionItemFactory
    {
        public Task<IUINode> CreateAsync(global::UIFramework.Mvvm.CollectionItemContext context, CancellationToken cancellationToken) =>
            context.LoadAsync("res://Scenes/Client/ShopRow.tscn", node => node.Bind("Player.Gold", EngineMvvm.BindingTargets.Property(((EngineUI.UIElement)node.Element!).Control, "SelfModulate"), new BindingOptions { Convert = value => (int)value! >= Catalog.Items[((ClientInventoryItemVm)context.Item).Id].Price ? Colors.White : new Color(.7f, .7f, .7f) }), cancellationToken);
    }
    private void UpdateInventory(string tab, System.Collections.ObjectModel.ObservableCollection<ClientInventoryItemVm> items)
    {
        _inventoryVm!.Update(items, Entries(tab).ToArray(), id =>
        {
            var row = new ClientInventoryItemVm { Id = id };
            row.Open = () => OpenInventoryAsync(tab, row); return row;
        }, row => { var (title, info, icon) = Describe(tab, row.Id); row.Title = title; row.Info = info; row.Icon = icon; });
    }
    private Task OpenInventoryAsync(string tab, ClientInventoryItemVm row)
    {
        var items = tab == "装备" ? _inventoryVm!.Equipment : _inventoryVm!.Materials;
        SelectedIcon = Get<Control>("Pages/" + tab).GetChild<Control>(items.IndexOf(row)).GetNode<TextureRect>("Icon");
        return App.RunAsync(() => App.Shell.Pages.PushAsync<ItemDetailsView>(Catalog.Items[row.Id].Id));
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
}
