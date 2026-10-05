using UIFramework.Mvvm;
using UnityEngine;
namespace NativeMvvm.Tests
{
    [ViewModel]
    public sealed class ConverterVm : ScriptableObject
    {
        [SerializeField] private int gold = 10;
        [FieldNotify] public int Gold { get => gold; set => gold = value; }
    }
}
