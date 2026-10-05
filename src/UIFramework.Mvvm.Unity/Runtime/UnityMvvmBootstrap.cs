using UIFramework.Unity;
using UnityEngine;

namespace UIFramework.Mvvm.Unity
{
    public static class UnityMvvmBootstrap
    {
        public static System.Action<MvvmRuntime> EditModeObserver { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Register() => MvvmModule.RegisterAdapter(typeof(UnityUIHost), new UnityViewModelAssets(),
            node => node.Element is UnityUIElement display ? display.Rect.GetComponent<MvvmBindings>() : null, runtime => { if (Application.isPlaying) UnityMvvmObserver.Attach(runtime); else EditModeObserver(runtime); });
    }
}
