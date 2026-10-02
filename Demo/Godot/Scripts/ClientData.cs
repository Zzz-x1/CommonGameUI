using System.Text.Json;
using System.Text.Json.Serialization;

namespace HierarchyDemo;
public sealed record Hero(string Name, string Role, string Story);
public sealed record Item(string Id, string Name, string Icon, int Power, int Price, string Description);
public sealed record Goal(string Name, string Action, int Target);
public static class Catalog
{
    public static readonly Hero[] Heroes =
    {
        new("艾琳", "晨光骑士", "守护星庭的银发骑士。相信每一次出发，都值得被温柔守候。"),
        new("诺亚", "林间游侠", "从翡翠森林而来的旅人，风与箭矢是他的同行者。"),
        new("莉瑟", "星辰法师", "观星塔的年轻学者，以星光书写未知世界的地图。"),
        new("苍岚", "巡夜守卫", "沉默的守卫，以盾牌为迷途的人们留下一处港湾。"),
        new("芙蕾", "花园炼金师", "收藏花香与旧配方，擅长把平凡日子变成小小奇迹。"),
        new("伊恩", "风吟诗人", "记录旅途的吟游诗人，故事总在下一阵风里。")
    };
    public static readonly string[] Icons =
    {
        "bow-arrow",
        "hood",
        "boots",
        "diamond-ring",
        "belt-armor",
        "compass",
        "crystal-growth",
        "rock",
        "herbs-bundle",
        "sewing-string",
        "amber-mosquito",
        "dead-wood"
    };
    public static readonly string[] EquipmentNames =
    {
        "月辉长弓",
        "巡风兜帽",
        "旅者之靴",
        "星砂指环",
        "晨曦护甲",
        "远行罗盘",
        "星晶法器",
        "山岳徽章",
        "花语护符",
        "银线披肩",
        "琥珀坠饰",
        "古木手杖"
    };
    public static readonly string[] MaterialNames =
    {
        "星辉结晶",
        "锻造矿石",
        "清露草",
        "织星丝",
        "琥珀碎片",
        "古木枝",
        "经验手记",
        "晨光花瓣"
    };
    public static readonly Item[] Items = EquipmentNames.Select((n, i) => new Item($"eq{i}", n, Icons[i], 30 + i * 8, 180 + i * 45, "星庭工坊制作的精良装备。装备后提升角色展示属性，可与其他装备并排比较。")).Concat(MaterialNames.Select((n, i) => new Item($"mat{i}", n, Icons[(i + 6) % 12], 0, 35 + i * 10, "旅途中收集的珍贵材料，用于角色培养与工坊收藏。"))).ToArray();
    public static Item Item(string id) => Items.Single(x => x.Id == id);
    public static readonly string[] Actions =
    {
        "login",
        "purchase",
        "equip",
        "train",
        "team",
        "mail",
        "favorite",
        "profile",
        "sign",
        "stage",
        "activity",
        "friend"
    };
    public static readonly string[] GoalNames =
    {
        "启程之日",
        "工坊访客",
        "整装待发",
        "星光成长",
        "结伴同行",
        "远方来信",
        "珍藏时刻",
        "新的名片",
        "晨间问候",
        "探索地图",
        "花园邀约",
        "同行者"
    };
    public static readonly Goal[] Tasks = GoalNames.Select((n, i) => new Goal(n, Actions[i], 1)).ToArray();
    public static readonly Goal[] Achievements = GoalNames.Select((n, i) => new Goal(n + " · 纪念", Actions[i], i is 0 or 7 or 8 or 10 ? 1 : 2)).ToArray();
    public static readonly string[] Stages =
    {
        "风起星庭",
        "花语小径",
        "翡翠林地",
        "月光渡口",
        "观星高塔",
        "黎明之门",
        "试炼 · 岩壁",
        "试炼 · 星火",
        "试炼 · 潮汐",
        "试炼 · 长夜"
    };
    public static readonly string[] MailTitles =
    {
        "欢迎来到星庭",
        "工坊补给已送达",
        "观星塔的邀请",
        "来自森林的问候",
        "巡夜队的谢礼",
        "花园庆典赠礼",
        "旅途准备清单",
        "吟游诗人的明信片"
    };
    public static readonly string[] Friends =
    {
        "白鹿",
        "晴空",
        "晚风",
        "星尘",
        "铃兰",
        "远山"
    };
    public static readonly string[] Events =
    {
        "花园庆典",
        "星光签到",
        "工坊采购季"
    };
}

public sealed class PlayerState
{
    [JsonRequired]
    public string Name { get; set; } = "旅行者";

    [JsonRequired]
    public int Gold { get; set; } = 12000;

    [JsonRequired]
    public int Gems { get; set; } = 600;

    [JsonRequired]
    public int Tokens { get; set; } = 50;

    [JsonRequired]
    public int Avatar { get; set; }

    [JsonRequired]
    public int Showcase { get; set; }

    [JsonRequired]
    public int[] Levels { get; set; } =
    {
        12,
        10,
        10,
        8,
        8,
        8
    };

    [JsonRequired]
    public string[] Equipment { get; set; } =
    {
        "eq0",
        "eq1",
        "eq6",
        "eq4",
        "eq8",
        "eq11"
    };

    [JsonRequired]
    public int[] Team { get; set; } =
    {
        0,
        1,
        -1,
        -1
    };

    [JsonRequired]
    public Dictionary<string, int> Inventory { get; set; } = Catalog.Items.ToDictionary(x => x.Id, x => x.Power > 0 ? 2 : 24);

    [JsonRequired]
    public Dictionary<string, int> Counts { get; set; } = Catalog.Actions.ToDictionary(x => x, _ => 0);

    [JsonRequired]
    public HashSet<string> Favorites { get; set; } = new();

    [JsonRequired]
    public HashSet<string> Claimed { get; set; } = new();

    [JsonRequired]
    public HashSet<int> ReadMail { get; set; } = new();

    [JsonRequired]
    public HashSet<int> DeletedMail { get; set; } = new();

    [JsonRequired]
    public HashSet<int> Friends { get; set; } = Enumerable.Range(0, 6).ToHashSet();

    [JsonRequired]
    public HashSet<int> FavoriteFriends { get; set; } = new();

    [JsonRequired]
    public double Volume { get; set; } = 70;

    [JsonRequired]
    public bool FullMotion { get; set; } = true;

    [JsonRequired]
    public bool Fullscreen { get; set; }

    [JsonRequired]
    public int Quality { get; set; } = 2;

    public int Power(int hero) => Levels[hero] * 25 + (Equipment[hero] == "" ? 0 : Catalog.Item(Equipment[hero]).Power);
}

/// <summary>Local client state. Publish only after the complete next snapshot has been saved.</summary>
public sealed class ClientStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly string _path;
    public PlayerState State { get; private set; }

    public event Action? Changed;
    public ClientStore(string path)
    {
        _path = path;
        State = File.Exists(path) ? JsonSerializer.Deserialize<PlayerState>(File.ReadAllText(path), Json)! : new PlayerState();
        Validate(State);
    }

    private static void Validate(PlayerState s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.Name) || s.Gold < 0 || s.Gems < 0 || s.Tokens < 0 || s.Levels.Length != 6 || s.Equipment.Length != 6 || s.Team.Length != 4 || s.Levels.Any(x => x < 1 || x > 60) || s.Avatar is < 0 or > 5 || s.Showcase is < 0 or > 5 || s.Team.Any(x => x < -1 || x > 5) || s.Team.Where(x => x >= 0).Distinct().Count() != s.Team.Count(x => x >= 0) || s.Volume is < 0 or > 100 || s.Quality is < 0 or > 2)
            throw new InvalidDataException("玩家存档包含非法状态。");
        if (!s.Inventory.Keys.ToHashSet().SetEquals(Catalog.Items.Select(x => x.Id)) || !s.Counts.Keys.ToHashSet().SetEquals(Catalog.Actions))
            throw new InvalidDataException("存档物品或行为目录不完整。");
        var rewards = Enumerable.Range(0, 12).SelectMany(i => new[] { $"t{i}", $"a{i}" })
            .Concat(Enumerable.Range(0, 8).Select(i => $"m{i}"))
            .Concat(Enumerable.Range(0, 3).Select(i => $"e{i}"));
        if (!s.Claimed.IsSubsetOf(rewards))
            throw new InvalidDataException("存档包含未知奖励。");
        foreach (var item in Catalog.Items)
            if (s.Inventory[item.Id] < 0)
                throw new InvalidDataException("物品数量不能为负。");
        foreach (var action in Catalog.Actions)
            if (s.Counts[action] < 0)
                throw new InvalidDataException("进度不能为负。");
        foreach (var id in s.Equipment.Where(x => x != ""))
            if (Catalog.Item(id).Power == 0)
                throw new InvalidDataException("材料不能装备。");
        foreach (var id in s.Favorites)
            Catalog.Item(id);
        if (s.Friends.Any(x => x < 0 || x >= 6) || !s.FavoriteFriends.IsSubsetOf(s.Friends) || s.ReadMail.Concat(s.DeletedMail).Any(x => x < 0 || x >= 8))
            throw new InvalidDataException("玩家记录包含未知条目。");
    }

    public void Change(Action<PlayerState> mutation)
    {
        var next = JsonSerializer.Deserialize<PlayerState>(JsonSerializer.Serialize(State, Json), Json)!;
        mutation(next);
        Validate(next);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(next, Json));
        File.Move(_path + ".tmp", _path, true);
        State = next;
        Changed?.Invoke();
    }

    public void Count(string action) => Change(s => s.Counts[action]++);
    public void Buy(string id, int quantity, bool bundle = false)
    {
        if (quantity < 1 || quantity > 10)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        var item = Catalog.Item(id);
        Change(s =>
        {
            var cost = checked(item.Price * quantity);
            if (s.Gold < cost)
                throw new InvalidOperationException("金币不足。");
            s.Gold -= cost;
            s.Inventory[id] += quantity;
            if (bundle)
                s.Inventory["mat6"] += 3 * quantity;
            s.Counts["purchase"]++;
        });
    }

    public void Train(int hero) => Change(s =>
    {
        if (s.Gold < 100 || s.Inventory["mat6"] < 1 || s.Levels[hero] >= 60)
            throw new InvalidOperationException("培养条件未满足。");
        s.Gold -= 100;
        s.Inventory["mat6"]--;
        s.Levels[hero]++;
        s.Counts["train"]++;
    });
    public void Equip(int hero, string id) => Change(s =>
    {
        if (s.Equipment[hero] == id)
            throw new InvalidOperationException("装备未发生变化。");
        if (id != "" && (Catalog.Item(id).Power == 0 || s.Inventory[id] <= s.Equipment.Count(x => x == id)))
            throw new InvalidOperationException("没有可用装备。");
        s.Equipment[hero] = id;
        if (id != "")
            s.Counts["equip"]++;
    });
    public void SetTeam(int slot, int hero) => Change(s =>
    {
        if (hero >= 0 && s.Team.Contains(hero))
            throw new InvalidOperationException("角色已在队伍中。");
        s.Team[slot] = hero;
        s.Counts["team"]++;
    });
    public void Favorite(string id) => Change(s =>
    {
        if (!s.Favorites.Add(id))
            s.Favorites.Remove(id);
        else
            s.Counts["favorite"]++;
    });
    public bool CanClaim(bool achievement, int id)
    {
        var goal = (achievement ? Catalog.Achievements : Catalog.Tasks)[id];
        return State.Counts[goal.Action] >= goal.Target && !State.Claimed.Contains($"{(achievement ? "a" : "t")}{id}");
    }

    public void ClaimGoal(bool achievement, int id)
    {
        if (!CanClaim(achievement, id))
            throw new InvalidOperationException("目标奖励不可领取。");
        Change(s =>
        {
            s.Claimed.Add($"{(achievement ? "a" : "t")}{id}");
            s.Gold += 200;
            s.Inventory["mat6"] += 2;
        });
    }

    public void ClaimMail(IEnumerable<int> ids)
    {
        var eligible = ids.Where(i => !State.DeletedMail.Contains(i) && !State.Claimed.Contains($"m{i}")).Distinct().ToArray();
        if (eligible.Length == 0)
            throw new InvalidOperationException("没有可领取附件。");
        Change(s =>
        {
            foreach (var i in eligible)
            {
                if (i < 0 || i >= 8)
                    throw new ArgumentOutOfRangeException(nameof(ids));
                s.Claimed.Add($"m{i}");
                s.ReadMail.Add(i);
                s.Gold += 300;
                s.Inventory["mat6"] += 2;
                s.Counts["mail"]++;
            }
        });
    }

    public bool CanClaimEvent(int id) => !State.Claimed.Contains($"e{id}") && (id < 2 || State.Counts["purchase"] >= 1);
    public void ClaimEvent(int id)
    {
        if (id is < 0 or > 2 || !CanClaimEvent(id))
            throw new InvalidOperationException("活动奖励不可领取。");
        Change(s =>
        {
            s.Claimed.Add($"e{id}");
            s.Gems += 50;
            s.Inventory["mat6"] += 3;
            s.Counts[id == 1 ? "sign" : "activity"]++;
        });
    }
}
