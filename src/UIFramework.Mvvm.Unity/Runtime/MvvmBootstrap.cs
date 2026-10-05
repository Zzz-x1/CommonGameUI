using EngineUI = global::UIFramework.Unity;
using UIFramework.Unity;
using UnityEngine;

namespace UIFramework.Mvvm.Unity
{
    public static class MvvmBootstrap
    {
        public static System.Action<MvvmRuntime> EditModeObserver { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Register() => MvvmModule.RegisterAdapter(typeof(EngineUI.UIHost), new ViewModelAssets(),
            node => node.Element is EngineUI.UIElement display ? display.Rect.GetComponent<MvvmBindings>() : null, runtime => { if (Application.isPlaying) MvvmObserver.Attach(runtime); else EditModeObserver(runtime); });
    }
}
