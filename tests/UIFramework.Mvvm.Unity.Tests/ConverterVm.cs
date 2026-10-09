using UIFramework.Mvvm;
using UIFramework.Mvvm.Unity;
using UnityEngine;
namespace NativeMvvm.Tests
{
    public sealed class ConverterVm : GlobalViewModel
    {
        [SerializeField] private int gold = 10;
        [SerializeField] private System.Collections.Generic.List<ConverterVm> items = new System.Collections.Generic.List<ConverterVm>();
        [FieldNotify] public System.Collections.Generic.List<ConverterVm> Items { get => items; set => items = value; }
        [Command] public void Purchase(int id) { Gold -= id; }
        [FieldNotify] public int Gold { get => gold; set => gold = value; }
    }
}
