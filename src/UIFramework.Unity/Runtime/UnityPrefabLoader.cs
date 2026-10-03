using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Unity
{
    public sealed class UnityPrefabLoader : IUIResourceLoader
    {
        private readonly UnityUIHost _host;
        public UnityPrefabLoader(UnityUIHost host) => _host = host ?? throw new ArgumentNullException(nameof(host));
        public async Task<IUIResource> LoadAsync(string resource, CancellationToken token)
        {
            _host.VerifyAccess();
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("A Resources prefab path is required.", nameof(resource));
            var request = Resources.LoadAsync<GameObject>(resource);
            // Unity cannot cancel ResourceRequest. Drain it before releasing this acquisition.
            while (!request.isDone) await _host.NextFrameAsync();
            token.ThrowIfCancellationRequested();
            var prefab = request.asset as GameObject;
            if (!prefab) throw new FileNotFoundException("UI prefab was not found in Resources.", resource);
            if (!(prefab.transform is RectTransform)) throw new InvalidOperationException("UI prefab must have a RectTransform root: " + resource);
            return new PrefabResource(_host, prefab);
        }
        private sealed class PrefabResource : IUIResource
        {
            private readonly UnityUIHost _host;
            private GameObject _prefab;
            public PrefabResource(UnityUIHost host, GameObject prefab) { _host = host; _prefab = prefab; }
            public IUIElement Instantiate(IUIElement mount)
            {
                _host.VerifyAccess();
                if (!_prefab) throw new ObjectDisposedException(nameof(PrefabResource));
                if (!(mount is UnityUIElement target) || target.Host != _host) throw new ArgumentException("Prefab needs a mount in the same Unity host.", nameof(mount));
                var instance = UnityEngine.Object.Instantiate(_prefab, target.Rect, false);
                try
                {
                    instance.SetActive(false);
                    return new UnityUIElement(_host, (RectTransform)instance.transform, owned: true);
                }
                catch (Exception ex)
                {
                    UnityUI.Cleanup(ex, () => { instance.transform.SetParent(null); UnityEngine.Object.Destroy(instance); });
                    throw;
                }
            }
            public void Dispose() { _host.VerifyAccess(); _prefab = null; }
        }
    }
}
