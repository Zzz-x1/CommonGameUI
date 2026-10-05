using UIFramework.Mvvm;

namespace HierarchyDemo;

[ViewModel]
public class ClientHeroVm
{
    [FieldNotify] public int Power { get; set; }
}
