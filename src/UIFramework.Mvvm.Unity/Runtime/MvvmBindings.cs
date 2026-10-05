using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UIFramework.Unity;

namespace UIFramework.Mvvm.Unity
{
    public enum ViewBindingKind { Property, Command, Collection }
    [Serializable] public sealed class ViewModelSourceData
    { public string Name = ""; public string TypeName = ""; public ViewModelCreation Creation; public ScriptableObject Template; }
    [Serializable] public sealed class ViewBindingData
    {
        public bool Enabled = true; public Component Target; public string Member = "text"; public string Source = "";
        public BindingMode Mode = BindingMode.OneWay; public ViewBindingKind Kind;
        public string Converter = "", ConvertBack = ""; public bool HasNullValue; public string NullValue = "";
        public GameObject ItemPrefab; public bool OneTimeCollection;
    }
    public sealed class MvvmBindings : MonoBehaviour, IViewBindingConfiguration
    {
        public List<ViewModelSourceData> ViewModels = new List<ViewModelSourceData>();
        public List<ViewBindingData> Bindings = new List<ViewBindingData>();
        public IReadOnlyList<ViewModelSource> Sources => ViewModels.Select(s => new ViewModelSource
        { Name = s.Name, Type = s.Template ? s.Template.GetType() : Type.GetType(s.TypeName, true), Creation = s.Creation, Template = s.Template }).ToArray();
        public void Apply(UINode node)
        {
            foreach (var binding in Bindings.Where(b => b.Enabled))
            {
                if (!binding.Target) throw new InvalidOperationException("Missing binding target: " + binding.Source);
                if (binding.Kind == ViewBindingKind.Command) node.BindCommand(binding.Source, ((Button)binding.Target).ClickTarget());
                else if (binding.Kind == ViewBindingKind.Collection) node.BindCollection(binding.Source, new UnityCollectionTarget(node, (RectTransform)binding.Target.transform, binding.ItemPrefab), binding.OneTimeCollection);
                else node.Bind(binding.Source, UnityBindingTargets.Property(binding.Target, binding.Member), new BindingOptions
                { Mode = binding.Mode, Convert = string.IsNullOrEmpty(binding.Converter) ? null : Converters.Find(binding.Converter), ConvertBack = string.IsNullOrEmpty(binding.ConvertBack) ? null : Converters.Find(binding.ConvertBack), HasNullValue = binding.HasNullValue, NullValue = binding.NullValue });
            }
        }
    }
    public static class UnityMvvmExtensions
    {
        public static T GetSharedViewModel<T>(this MvvmRuntime runtime, T template) where T : ScriptableObject => runtime.GetSharedViewModel(template, new UnityViewModelAssets());
        public static void UseUnityMvvm(this UIContext ui, MvvmRuntime runtime)
        {
            ui.AddNodeExtension(new MvvmNodeExtension(runtime, node => node.Element is UnityUIElement display ? display.Rect.GetComponent<MvvmBindings>() : null, new UnityViewModelAssets()));
            UnityMvvmObserver.Attach(runtime);
        }
    }
}
