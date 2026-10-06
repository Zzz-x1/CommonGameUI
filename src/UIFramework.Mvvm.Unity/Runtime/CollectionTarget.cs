using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UIFramework.Unity;
using CoreNode = global::UIFramework.UINode;
namespace UIFramework.Mvvm.Unity
{
    public sealed class CollectionTarget : CollectionTargetBase
    {
        private readonly IUINode _owner; private readonly RectTransform _mount; private readonly GameObject _template;
        private readonly bool _preview; private readonly ICollectionItemFactory _factory;
        private readonly IReadOnlyList<CollectionSourceMapping> _sources; private int _id;
        private readonly string _name = "Items_" + Guid.NewGuid().ToString("N");
        public CollectionTarget(IUINode owner, RectTransform mount, GameObject template, bool preview = false,
            ICollectionItemFactory factory = null, IReadOnlyList<CollectionSourceMapping> sources = null) : base(owner)
        { _owner = owner; _mount = mount; _template = template; _preview = preview; _factory = factory; _sources = sources ?? Array.Empty<CollectionSourceMapping>(); if (!template && factory == null) throw new ArgumentException("An item resource or factory is required."); }
        protected override global::UIFramework.Mvvm.CollectionItemContext CreateContext(object item, int index) =>
            new NativeItemContext(_owner, item, index, _preview, _mount, _sources, _name + "_" + _id++);
        protected override Task<IUINode> CreateItemAsync(global::UIFramework.Mvvm.CollectionItemContext context, CancellationToken token) =>
            _factory == null ? context.CreateAsync(_template, cancellationToken: token) : _factory.CreateAsync(context, token);
        protected override void Place(IUINode node, int index) => ((UIElement)node.Element).Rect.SetSiblingIndex(index);
        protected override void Detach(IUINode node) { var rect = ((UIElement)node.Element).Rect; rect.gameObject.SetActive(false); rect.SetParent(null, false); }
    }
    internal sealed class NativeItemContext : global::UIFramework.Mvvm.CollectionItemContext
    {
        private readonly UIHost _host; private GameObject _instance;
        internal NativeItemContext(IUINode owner, object item, int index, bool preview, RectTransform mount,
            IReadOnlyList<CollectionSourceMapping> sources, string name) : base(owner, item, index, preview, new UIElement((UIHost)owner.Context.Host, mount), sources, name)
        { _host = (UIHost)owner.Context.Host; }
        internal Task<IUINode> CreateAsync(GameObject prefab, Action<IUINode> configure, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); _instance = UnityEngine.Object.Instantiate(prefab, ((UIElement)Mount).Rect, false);
            if (IsPreview) foreach (var child in _instance.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            _instance.SetActive(true); _instance.SetActive(false);
            return AttachAsync(new UIElement(_host, (RectTransform)_instance.transform, owned: !IsPreview), configure, token);
        }
        protected override IUINode ResolveNode(IUIElement element)
        {
            var rect = ((UIElement)element).Rect; rect.name = rect.name.Replace("(Clone)", "") + Index;
            if (IsPreview) return new CoreNode();
            var scripts = ((UIElement)element).Rect.GetComponents<global::UIFramework.Unity.UINode>();
            if (scripts.Length > 1) throw new InvalidOperationException("Collection item root has multiple UI scripts.");
            return scripts.Length == 0 ? (IUINode)new CoreNode() : scripts[0];
        }
        protected override void ReleaseNativeDisplay() { if (IsPreview && _instance) UnityEngine.Object.DestroyImmediate(_instance); }
    }
    public static class CollectionItemExtensions
    {
        public static Task<IUINode> CreateAsync(this global::UIFramework.Mvvm.CollectionItemContext context, GameObject prefab,
            Action<IUINode> configure = null, CancellationToken cancellationToken = default) => ((NativeItemContext)context).CreateAsync(prefab, configure, cancellationToken);
    }
}
