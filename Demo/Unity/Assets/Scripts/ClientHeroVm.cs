using UIFramework.Mvvm;

namespace StarCourt.UnityDemo
{
    [ViewModel]
    public class ClientHeroVm
    {
        [FieldNotify] public int Power { get; set; }
    }
}
