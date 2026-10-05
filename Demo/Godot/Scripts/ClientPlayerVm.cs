using Godot;
using UIFramework.Mvvm;
namespace HierarchyDemo;
[ViewModel]
public partial class ClientPlayerVm : Resource
{
    [Export, FieldNotify] public int Gold { get; set; }
    [Export, FieldNotify] public int Gems { get; set; }
    [Export, FieldNotify] public int Tokens { get; set; }
    [FieldNotify(nameof(Gold), nameof(Gems), nameof(Tokens))]
    public string Wallet => $"金币  {Gold:N0}     星石  {Gems}     徽章  {Tokens}";
    public void Refresh(PlayerState state) { Gold = state.Gold; Gems = state.Gems; Tokens = state.Tokens; }
}
[ViewModel]
public class ClientSettingsVm
{
    private float _volume;
    private bool _fullscreen, _motion;
    private ClientStore _store = null!;
    private Action _apply = null!;
    public void Initialize(ClientStore store, Action apply) { _store = store; _apply = apply; _volume = (float)store.State.Volume; _fullscreen = store.State.Fullscreen; _motion = store.State.FullMotion; }
    [FieldNotify] public float Volume { get => _volume; set { if (_volume == value) return; _store.Change(s => s.Volume = value); _volume = value; _apply(); } }
    [FieldNotify] public bool Fullscreen { get => _fullscreen; set { if (_fullscreen == value) return; _store.Change(s => s.Fullscreen = value); _fullscreen = value; _apply(); } }
    [FieldNotify] public bool FullMotion { get => _motion; set { if (_motion == value) return; _store.Change(s => s.FullMotion = value); _motion = value; } }
}
