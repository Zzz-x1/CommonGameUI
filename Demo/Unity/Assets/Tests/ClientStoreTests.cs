using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;

namespace StarCourt.UnityDemo.Tests
{
    public sealed class ClientStoreTests
    {
        private string _folder, _path;
        [SetUp] public void SetUp()
        { _folder = Path.Combine(Path.GetTempPath(), "StarCourtTests-" + Guid.NewGuid()); Directory.CreateDirectory(_folder); _path = Path.Combine(_folder, "save.json"); }
        [TearDown] public void TearDown() => Directory.Delete(_folder, true);
        [Test] public void PurchaseEquipTrainClaimAndRestart()
        {
            var store = new ClientStore(_path); store.Buy(0, 2); store.Equip(0, 0); store.Train(0); store.ClaimQuest();
            var restarted = new ClientStore(_path);
            Assert.That(restarted.State.Gold, Is.EqualTo(12140)); Assert.That(restarted.State.Inventory[0], Is.EqualTo(4));
            Assert.That(restarted.State.Levels[0], Is.EqualTo(13)); Assert.That(restarted.State.Equipment[0], Is.EqualTo(0));
            Assert.That(restarted.State.Materials, Is.EqualTo(26)); Assert.That(restarted.State.QuestClaimed, Is.True);
            Assert.Throws<InvalidOperationException>(() => restarted.ClaimQuest());
        }
        [Test] public void DuplicateTeamAndInsufficientMoneyDoNotCommit()
        {
            var store = new ClientStore(_path); var previous = store.State;
            Assert.Throws<InvalidDataException>(() => store.Change(s => s.Team[2] = 0));
            Assert.Throws<InvalidOperationException>(() => store.Buy(7, 99)); Assert.That(store.State, Is.SameAs(previous)); Assert.That(File.Exists(_path), Is.False);
        }
        [Test] public void MissingFieldsAndMalformedSavesFail()
        {
            File.WriteAllText(_path, "{}"); Assert.Throws<JsonSerializationException>(() => new ClientStore(_path));
            File.WriteAllText(_path, "not json"); Assert.Throws<JsonReaderException>(() => new ClientStore(_path));
        }
        [Test] public void WriteFailureLeavesPublishedSnapshotUntouched()
        {
            var store = new ClientStore(_path); var previous = store.State; Directory.CreateDirectory(_path + ".tmp");
            Assert.Throws<UnauthorizedAccessException>(() => store.Buy(0, 1)); Assert.That(store.State, Is.SameAs(previous));
        }
        [Test] public void BusinessDisabledEquipmentLimitIsEnforcedByModel()
        {
            var store = new ClientStore(_path); store.Equip(0, 0); store.Equip(1, 0);
            Assert.Throws<InvalidOperationException>(() => store.Equip(2, 0)); store.Equip(0, -1); store.Equip(2, 0);
            Assert.That(store.State.Equipment[2], Is.EqualTo(0));
        }
    }
}
