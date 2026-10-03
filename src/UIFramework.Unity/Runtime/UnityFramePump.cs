using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Unity
{
    // Completion runs in Update on Unity's thread; no worker-thread polling or async void.
    public sealed class UnityFramePump : MonoBehaviour
    {
        private readonly List<TaskCompletionSource<bool>> _pending = new List<TaskCompletionSource<bool>>();
        public Task NextFrameAsync()
        {
            if (!this || !isActiveAndEnabled)
                throw new InvalidOperationException("The UI frame pump must be alive and enabled.");
            var completion = new TaskCompletionSource<bool>();
            _pending.Add(completion);
            return completion.Task;
        }
        private void Update()
        {
            var pending = _pending.ToArray();
            _pending.Clear();
            foreach (var completion in pending) completion.SetResult(true);
        }
        private void OnDisable()
        {
            var pending = _pending.ToArray();
            _pending.Clear();
            foreach (var completion in pending)
                completion.SetException(new ObjectDisposedException(nameof(UnityFramePump)));
        }
    }
}
