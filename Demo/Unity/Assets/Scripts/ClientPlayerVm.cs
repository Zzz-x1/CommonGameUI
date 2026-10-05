using UnityEngine;
using UIFramework.Mvvm;
namespace StarCourt.UnityDemo
{
    [ViewModel]
    public sealed class ClientPlayerVm : ScriptableObject
    {
        [SerializeField] private int gold, materials;
        [FieldNotify] public int Gold { get => gold; set => gold = value; }
        [FieldNotify] public int Materials { get => materials; set => materials = value; }
        [FieldNotify(nameof(Gold), nameof(Materials))] public string Wallet => "金币 " + Gold.ToString("N0") + "    经验手记 " + Materials;
        public void Refresh(PlayerState state) { Gold = state.Gold; Materials = state.Materials; }
    }
    [ViewModel]
    public class ClientSettingsVm
    {
        private ClientStore _store;
        private bool _motion;
        public void Initialize(ClientStore store) { _store = store; _motion = store.State.FullMotion; }
        [FieldNotify] public bool FullMotion { get => _motion; set { if (_motion == value) return; _store.Change(s => s.FullMotion = value); _motion = value; } }
    }
}
