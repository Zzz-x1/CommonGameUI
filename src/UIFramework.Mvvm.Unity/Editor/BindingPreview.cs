using EngineMvvm = global::UIFramework.Mvvm.Unity;
using EngineUI = global::UIFramework.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UIFramework.Unity;
using UIFramework.Mvvm.Unity;

namespace UIFramework.Mvvm.EditorUnity
{
    public sealed class BindingPreview : IDisposable
    {
        private readonly MvvmRuntime _runtime = new MvvmRuntime();
        private readonly List<(IBindingTarget Target, object Value)> _values = new List<(IBindingTarget, object)>();
        private readonly List<(GameObject Object, bool Active)> _children = new List<(GameObject, bool)>();
        private readonly GameObject _host;
        private readonly UIContext _ui;
        private bool _disposed;
        private sealed class SourceConfiguration : IViewBindingConfiguration
        {
            public IReadOnlyList<ViewModelSource> SourcesList;
            public IReadOnlyList<ViewModelSource> Sources => SourcesList;
            public void Apply(UINode node) { }
        }
        private sealed class ItemConfiguration : IViewBindingConfiguration
        {
            public MvvmBindings View;
            public IReadOnlyList<ViewModelSource> Sources => View.Sources.Select(s => new ViewModelSource { Name = s.Name, Type = s.Type, Creation = s.Creation == ViewModelCreation.Manual ? ViewModelCreation.Manual : ViewModelCreation.CreateInstance, Template = s.Template }).ToArray();
            public void Apply(UINode node)
            {
                foreach (var binding in View.Bindings.Where(b => b.Enabled && b.Kind == ViewBindingKind.Property))
                {
                    var options = binding.GetOptions(true);
                    node.Bind(binding.Source, EngineMvvm.BindingTargets.Property(binding.Target, binding.Member), options);
                }
            }
        }
        public BindingPreview(MvvmBindings view)
        {
            var sources = view.Sources.Select(s => new ViewModelSource { Name = s.Name, Type = s.Type, Template = s.Template, Creation = ViewModelCreation.CreateInstance }).ToArray();
            _host = new GameObject("MVVM Preview Host", typeof(EventSystem), typeof(EngineUI.FramePump)) { hideFlags = HideFlags.HideAndDontSave };
            var host = new EngineUI.UIHost(_host.GetComponent<EventSystem>(), _host.GetComponent<EngineUI.FramePump>());
            _ui = new UIContext(host, new EngineUI.UIResourceLoader(host));
            _ui.ConfigureMvvm(_runtime, node => node.Name == "Preview" ? (IViewBindingConfiguration)new SourceConfiguration { SourcesList = sources }
                : node.Element is EngineUI.UIElement display && display.Rect.TryGetComponent<MvvmBindings>(out var configuration) ? new ItemConfiguration { View = configuration } : null, new EngineMvvm.ViewModelAssets());
            try
            {
                var node = new UINode(); var attached = node.AttachAsync(_ui.Root, "Preview");
                if (!attached.IsCompleted) throw new InvalidOperationException("Preview initialization must be immediate."); attached.GetAwaiter().GetResult();
                foreach (var b in view.Bindings.Where(b => b.Enabled && b.Kind != ViewBindingKind.Command))
                {
                    if (b.Kind == ViewBindingKind.Collection)
                    {
                        foreach (Transform child in b.Target.transform) { _children.Add((child.gameObject, child.gameObject.activeSelf)); child.gameObject.SetActive(false); }
                        node.BindCollection(b.Source, new EngineMvvm.CollectionTarget(node, (RectTransform)b.Target.transform, b.ItemPrefab, true), b.OneTimeCollection);
                    }
                    else
                    {
                        var options = b.GetOptions(true); var target = EngineMvvm.BindingTargets.Property(b.Target, b.Member); _values.Add((target, target.Read()));
                        node.Bind(b.Source, target, options);
                    }
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; EditorMvvmTasks.Observe(Close());
        }
        private async Task Close()
        {
            Exception first = null;
            try { await _ui.DisposeAsync(); } catch (Exception e) { first = e; }
            foreach (var value in _values)
                try { value.Target.Write(value.Value); } catch (Exception e) { if (first == null) first = e; else first.Data["MVVM.Preview.RestoreError"] = e; }
            foreach (var child in _children) if (child.Object) child.Object.SetActive(child.Active);
            UnityEngine.Object.DestroyImmediate(_host);
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
    }
    [InitializeOnLoad]
    internal static class EditorMvvmTasks
    {
        private static readonly List<Task> Tasks = new List<Task>();
        static EditorMvvmTasks() { EditorApplication.update += () => { foreach (var task in Tasks.ToArray()) if (task.IsCompleted) { Tasks.Remove(task); task.GetAwaiter().GetResult(); } }; }
        public static void Observe(Task task) => Tasks.Add(task);
    }
}
