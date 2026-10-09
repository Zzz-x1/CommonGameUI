using UnityEngine;
using System.Threading.Tasks;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
namespace StarCourt.UnityDemo
{
    public sealed class ClientPlayerVm : GlobalViewModel
    {
        [SerializeField] private int gold, materials;
        [FieldNotify] public int Gold { get => gold; set => gold = value; }
        [FieldNotify] public int Materials { get => materials; set => materials = value; }
        [FieldNotify(nameof(Gold), nameof(Materials))] public string Wallet => "金币 " + Gold.ToString("N0") + "    经验手记 " + Materials;
        private ClientStore _store;
        protected override void OnInitialize()
        {
            _store = UnityClient.Instance.Store;
            _store.Changed += Refresh;
            Refresh();
        }
        private void Refresh() => Refresh(_store.State);
        protected override ValueTask OnDisposeAsync()
        {
            if (_store != null) _store.Changed -= Refresh;
            return default;
        }
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
