using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UIFramework.Mvvm.Unity
{
    public sealed class UnityMvvmObserver : MonoBehaviour
    {
        private static UnityMvvmObserver _instance; private readonly HashSet<MvvmRuntime> _runtimes = new HashSet<MvvmRuntime>();
        internal static void Attach(MvvmRuntime runtime)
        {
            if (!_instance)
            { var go = new GameObject("[UIFramework] MVVM") { hideFlags = HideFlags.HideInHierarchy }; DontDestroyOnLoad(go); _instance = go.AddComponent<UnityMvvmObserver>(); }
            _instance._runtimes.Add(runtime);
        }
        private void Update() { foreach (var runtime in _runtimes.ToArray()) { if (runtime.IsDisposed) _runtimes.Remove(runtime); else runtime.ObserveTasks(); } }
    }
}
