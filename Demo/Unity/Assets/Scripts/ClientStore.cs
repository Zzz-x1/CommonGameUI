using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace StarCourt.UnityDemo
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PlayerState
    {
        [JsonProperty(Required = Required.Always)] public int Gold = 12000;
        [JsonProperty(Required = Required.Always)] public int Materials = 24;
        [JsonProperty(Required = Required.Always)] public int[] Inventory = Enumerable.Repeat(2, 8).ToArray();
        [JsonProperty(Required = Required.Always)] public int[] Levels = Enumerable.Repeat(12, 6).ToArray();
        [JsonProperty(Required = Required.Always)] public int[] Equipment = Enumerable.Repeat(-1, 6).ToArray();
        [JsonProperty(Required = Required.Always)] public int[] Team = new[] { 0, 1, -1, -1 };
        [JsonProperty(Required = Required.Always)] public bool FullMotion = true;
        [JsonProperty(Required = Required.Always)] public int Quality = 2;
        [JsonProperty(Required = Required.Always)] public bool QuestClaimed;
        [JsonProperty(Required = Required.Always)] public int Purchases;
    }
    public sealed class ClientStore
    {
        public static readonly string[] Heroes = { "艾琳", "祈星", "莉娜", "黎鸢", "蕾雅", "晨歌" };
        public static readonly string[] Items = { "古木手杖", "月辉徽章", "巡风兜帽", "旅者长靴", "星砂指环", "晨曦护符", "远行披甲", "星晶法器" };
        public static readonly int[] Prices = { 180, 225, 270, 315, 360, 405, 450, 495 };
        public PlayerState State { get; private set; }
        public event Action Changed;
        private readonly string _path;
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
        public ClientStore(string path)
        {
            _path = path;
            State = File.Exists(path) ? JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(path), Json) : new PlayerState();
            Validate(State);
        }
        private static void Validate(PlayerState s)
        {
            if (s == null || s.Gold < 0 || s.Materials < 0 || s.Purchases < 0 || s.Quality < 0 || s.Quality > 2
                || s.Inventory == null || s.Inventory.Length != 8 || s.Inventory.Any(i => i < 0)
                || s.Levels == null || s.Levels.Length != 6 || s.Levels.Any(i => i < 1)
                || s.Equipment == null || s.Equipment.Length != 6 || s.Equipment.Any(i => i < -1 || i >= 8)
                || s.Team == null || s.Team.Length != 4 || s.Team.Any(i => i < -1 || i >= 6)
                || s.Team.Where(i => i >= 0).Distinct().Count() != s.Team.Count(i => i >= 0))
                throw new InvalidDataException("Invalid Unity client save.");
            for (int i = 0; i < s.Inventory.Length; i++)
                if (s.Equipment.Count(item => item == i) > s.Inventory[i]) throw new InvalidDataException("Equipped items exceed owned inventory.");
        }
        public void Change(Action<PlayerState> change)
        {
            var next = JsonConvert.DeserializeObject<PlayerState>(JsonConvert.SerializeObject(State), Json);
            change(next);
            Validate(next);
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path + ".tmp", JsonConvert.SerializeObject(next, Formatting.Indented));
            if (File.Exists(_path)) File.Replace(_path + ".tmp", _path, null);
            else File.Move(_path + ".tmp", _path);
            State = next;
            Changed?.Invoke();
        }
        public void Buy(int item, int quantity)
        {
            if (item < 0 || item >= Items.Length || quantity < 1 || quantity > 99) throw new ArgumentOutOfRangeException(nameof(item));
            Change(s =>
            {
                var price = checked(Prices[item] * quantity);
                if (s.Gold < price) throw new InvalidOperationException("Insufficient gold.");
                s.Gold -= price; s.Inventory[item] += quantity; s.Purchases++;
            });
        }
        public void Train(int hero)
        {
            Change(s =>
            {
                if (s.Materials < 2 || s.Gold < 300) throw new InvalidOperationException("Insufficient training materials.");
                s.Materials -= 2; s.Gold -= 300; s.Levels[hero]++;
            });
        }
        public void Equip(int hero, int item)
        {
            Change(s =>
            {
                if (item >= 0 && s.Inventory[item] <= s.Equipment.Count(i => i == item) && s.Equipment[hero] != item)
                    throw new InvalidOperationException("No unequipped copy remains.");
                s.Equipment[hero] = item;
            });
        }
        public void ClaimQuest()
        {
            Change(s =>
            {
                if (s.Purchases < 1 || s.QuestClaimed) throw new InvalidOperationException("Quest is not claimable.");
                s.QuestClaimed = true; s.Gold += 800; s.Materials += 4;
            });
        }
    }
}
