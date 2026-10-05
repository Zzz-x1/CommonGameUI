using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UIFramework.Unity;
namespace UIFramework.Mvvm.Unity
{
    public sealed class UnityCollectionTarget : ICollectionTarget
    {
        private readonly UINode _owner; private readonly RectTransform _mount; private readonly GameObject _template;
        private readonly List<(GameObject Object, UINode Node)> _items = new List<(GameObject, UINode)>(); private int _id;
        private readonly bool _preview;
        public UnityCollectionTarget(UINode owner, RectTransform mount, GameObject template, bool preview = false)
        { _owner = owner; _mount = mount; _preview = preview; _template = template ? template : throw new ArgumentNullException(nameof(template)); }
        public void Insert(int index, object item)
        {
            var go = UnityEngine.Object.Instantiate(_template, _mount, false); var node = new ItemNode(item);
            if (_preview) foreach (var child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            var attach = node.AttachToAsync(_owner, "Item" + _id++, (RectTransform)go.transform);
            if (!attach.IsCompleted) throw new InvalidOperationException("List item initialization must be synchronous."); attach.GetAwaiter().GetResult();
            index = index == int.MaxValue ? _items.Count : index; _items.Insert(index, (go, node)); go.transform.SetSiblingIndex(index);
        }
        public void Remove(int index)
        {
            var item = _items[index]; _items.RemoveAt(index); if (item.Object) { item.Object.SetActive(false); item.Object.transform.SetParent(null); }
            _owner.ObserveMvvmTask(Release());
            async Task Release()
            {
                try { await item.Node.DisposeAsync(); }
                finally { if (item.Object) { if (_preview) UnityEngine.Object.DestroyImmediate(item.Object); else UnityEngine.Object.Destroy(item.Object); } }
            }
        }
        public void Move(int oldIndex, int newIndex) { var item = _items[oldIndex]; _items.RemoveAt(oldIndex); _items.Insert(newIndex, item); item.Object.transform.SetSiblingIndex(newIndex); }
        public void Clear() { for (int i = _items.Count - 1; i >= 0; i--) Remove(i); }
        public void Dispose() => Clear();
        private sealed class ItemNode : UINode
        {
            private readonly object _vm; public ItemNode(object vm) { _vm = vm; }
            protected override bool InitiallyVisible => true;
            protected override Task OnInitializeAsync(CancellationToken token) { this.SetViewModel("Item", _vm); return Task.CompletedTask; }
        }
    }
}
