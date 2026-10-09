using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Mvvm.Unity
{
    public abstract class GlobalViewModel : ScriptableObject
    {
        [NonSerialized] private bool _runtimeInstance;
        [NonSerialized] private bool _preview;
        [NonSerialized] private bool _initializationStarted;
        [NonSerialized] private Task _disposal;
        internal bool IsRuntimeInstance => _runtimeInstance;
        internal void PrepareRuntime(bool preview) { _runtimeInstance = true; _preview = preview; }
        internal void InitializeRuntime()
        {
            if (!_runtimeInstance) throw new InvalidOperationException("Only runtime GlobalViewModel clones can be initialized: " + name);
            if (_preview) return;
            if (_initializationStarted) throw new InvalidOperationException("Global VM initialization already started: " + name);
            _initializationStarted = true;
            OnInitialize();
        }
        internal ValueTask DisposeRuntimeAsync()
        {
            if (!_runtimeInstance || _preview) return default;
            if (_disposal == null) _disposal = OnDisposeAsync().AsTask();
            return new ValueTask(_disposal);
        }
        protected virtual void OnInitialize() { }
        protected virtual ValueTask OnDisposeAsync() => default;
    }
}
