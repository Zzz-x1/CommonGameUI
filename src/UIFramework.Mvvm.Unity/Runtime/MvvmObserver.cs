using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UIFramework.Mvvm.Unity
{
    public sealed class MvvmObserver : MonoBehaviour
    {
        private static MvvmObserver _instance; private readonly HashSet<MvvmRuntime> _runtimes = new HashSet<MvvmRuntime>();
        internal static void Attach(MvvmRuntime runtime)
        {
            if (!_instance)
            { var go = new GameObject("[UIFramework] MVVM") { hideFlags = HideFlags.HideInHierarchy }; DontDestroyOnLoad(go); _instance = go.AddComponent<MvvmObserver>(); }
            _instance._runtimes.Add(runtime);
        }
        private void Update() { foreach (var runtime in _runtimes.ToArray()) { runtime.ObserveTasks(); if (runtime.IsDisposed) _runtimes.Remove(runtime); } }
    }
}
