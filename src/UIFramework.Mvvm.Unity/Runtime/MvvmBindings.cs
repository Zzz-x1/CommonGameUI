using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UIFramework.Unity;

namespace UIFramework.Mvvm.Unity
{
    public enum ViewBindingKind { Property, Command, Collection }
    [Serializable] public sealed class ViewBindingData
    {
        public ScriptableObject ViewModel; public bool IsGlobal;
        public bool Enabled = true; public Component Target; public string Member = "text"; public string Source = "";
        public BindingMode Mode = BindingMode.OneWay; public ViewBindingKind Kind;
        public string Converter = "", ConvertBack = ""; public ConverterInput ConverterInput; public string ConverterConstant = ""; public bool HasNullValue; public string NullValue = "";
        public GameObject ItemPrefab; public bool OneTimeCollection; public Component ItemFactory;
        public List<CollectionSourceMapping> ItemSources = new List<CollectionSourceMapping>();
        public bool ParameterIsNull; public CommandParameterMode ParameterMode; public string ParameterConstant = "", ParameterPath = "";
        public CommandBindingOptions GetCommandOptions() => ParameterMode == CommandParameterMode.SourcePath
            ? new CommandBindingOptions { ParameterPath = ParameterPath }
            : new CommandBindingOptions { Mode = ParameterMode, ConstantText = ParameterMode == CommandParameterMode.Constant && !ParameterIsNull ? ParameterConstant : null };
        public BindingOptions GetOptions(bool preview = false)
        {
            var options = Converters.Configure(Converter, ConvertBack, Mode, ConverterInput, ConverterConstant, HasNullValue, NullValue);
            if (preview) { options.Mode = Mode == BindingMode.OneTime ? BindingMode.OneTime : BindingMode.OneWay; options.ConvertBack = null; }
            return options;
        }
    }
    public sealed class MvvmBindings : MonoBehaviour, IViewBindingConfiguration
    {
        public List<ViewBindingData> Bindings = new List<ViewBindingData>();
        public IReadOnlyList<ViewModelSource> Sources
        {
            get
            {
                var bindings = Bindings.Where(binding => binding.Enabled).ToArray();
                foreach (var binding in bindings.Where(binding => binding.IsGlobal))
                {
                    if (!binding.ViewModel) throw new InvalidOperationException("Missing global VM asset: " + binding.Source);
                    if (binding.Source.Split('.')[0] == "Item") throw new InvalidOperationException("Item is a collection-injected source, not a global VM.");
                }
                return bindings.Where(binding => binding.ViewModel).GroupBy(binding => binding.Source.Split('.')[0]).Select(group =>
                {
                    var binding = group.First();
                    if (group.Any(other => other.ViewModel != binding.ViewModel || other.IsGlobal != binding.IsGlobal))
                        throw new InvalidOperationException("Conflicting VM references for source: " + group.Key);
                    return new ViewModelSource { Name = group.Key, Type = binding.ViewModel.GetType(),
                        Template = binding.ViewModel, Creation = ViewModelCreation.Manual, IsGlobal = binding.IsGlobal };
                }).ToArray();
            }
        }
        public CollectionTarget GetCollectionTarget(IUINode node, ViewBindingData binding, bool preview = false) =>
            new CollectionTarget(node, (RectTransform)binding.Target.transform, binding.ItemPrefab, preview,
                binding.ItemFactory ? (ICollectionItemFactory)binding.ItemFactory : null, binding.ItemSources);
        public void Apply(UINode node) => Apply(node, false);
        public void Apply(UINode node, bool preview)
        {
            foreach (var binding in Bindings.Where(b => b.Enabled))
            {
                if (!binding.Target) throw new InvalidOperationException("Missing binding target: " + binding.Source);
                if (binding.Kind == ViewBindingKind.Command) { if (preview) continue; node.BindCommand(binding.Source, ((Button)binding.Target).ClickTarget(), binding.GetCommandOptions()); }
                else if (binding.Kind == ViewBindingKind.Collection) node.BindCollection(binding.Source, GetCollectionTarget(node, binding, preview), binding.OneTimeCollection);
                else { var options = binding.GetOptions(preview); node.Bind(binding.Source, BindingTargets.Property(binding.Target, binding.Member, options.Mode), options); }
            }
        }
    }
    public static class MvvmExtensions
    {
        public static T GetSharedViewModel<T>(this UIContext ui, T template) where T : ScriptableObject => ui.GetMvvm().GetSharedViewModel(template, new ViewModelAssets());
    public static T GetSharedViewModel<T>(this MvvmRuntime runtime, T template) where T : ScriptableObject => runtime.GetSharedViewModel(template, new ViewModelAssets());

    }
}
