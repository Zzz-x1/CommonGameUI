using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UIFramework.Unity;

namespace UIFramework.Mvvm.Unity
{
    [Serializable] public sealed class ViewBindingData
    {
        public bool Enabled = true; public Component Target; public string Member = "text"; public string Source = "";
        public BindingMode Mode = BindingMode.OneWay; public ViewBindingKind Kind;
        public BindingUpdateTiming UpdateTiming = BindingUpdateTiming.EndOfFrame;
        public string Converter = "", ConvertBack = ""; public ConverterInput ConverterInput; public string ConverterConstant = ""; public bool HasNullValue; public string NullValue = "";
        public GameObject ItemPrefab; public bool OneTimeCollection; public Component ItemFactory;
        public List<CollectionSourceMapping> ItemSources = new List<CollectionSourceMapping>();
        public bool ParameterIsNull; public CommandParameterMode ParameterMode; public string ParameterConstant = "", ParameterPath = "";
        public BindingRule Rule => new BindingRule(Kind, Source, Member, Mode, Converter, ConvertBack, ConverterInput,
            ConverterConstant, HasNullValue, NullValue, ParameterMode, ParameterIsNull ? null : ParameterConstant,
            ParameterPath, OneTimeCollection, UpdateTiming);
    }
    public sealed class MvvmBindings : MonoBehaviour, IViewBindingConfiguration, IViewBindingSlots
    {
        public List<ViewBindingData> Bindings = new List<ViewBindingData>();
        public IReadOnlyList<ViewModelSource> Sources
        {
            get
            {
                return SourceNames().Distinct(StringComparer.Ordinal)
                    .Select(name => new ViewModelSource { Name = name, Creation = ViewModelCreation.Manual }).ToArray();
            }
        }
        private IEnumerable<string> SourceNames()
        {
            foreach (var row in Bindings)
            {
                if (!row.Enabled || row.Kind == ViewBindingKind.Property && row.ConverterInput == ConverterInput.Constant) continue;
                yield return SourceName(row.Source);
                if (row.Kind == ViewBindingKind.Command && row.ParameterMode == CommandParameterMode.SourcePath)
                    yield return SourceName(row.ParameterPath);
            }
        }
        private static string SourceName(string path)
        {
            var end = path.IndexOf('.');
            return end < 0 ? path : path.Substring(0, end);
        }
        int IViewBindingSlots.Count => Bindings.Count;
        bool IViewBindingSlots.IsEnabled(int slot) => Bindings[slot].Enabled;
        BindingRule IViewBindingSlots.GetRule(int slot) => Bindings[slot].Rule;
        object IViewBindingSlots.GetTarget(int slot) => Bindings[slot].Target ? Bindings[slot].Target
            : throw new InvalidOperationException("Missing binding target: " + Bindings[slot].Source);
        Type IViewBindingSlots.CommandTargetType => typeof(Button);
        ICommandTarget IViewBindingSlots.CreateCommandTarget(object target) => ((Button)target).ClickTarget();
        ICollectionTarget IViewBindingSlots.CreateCollectionTarget(IUINode node, int slot, bool preview)
        {
            var binding = Bindings[slot];
            return new CollectionTarget(node, (RectTransform)binding.Target.transform, binding.ItemPrefab, preview,
                binding.ItemFactory ? (ICollectionItemFactory)binding.ItemFactory : null, binding.ItemSources);
        }
        public void Apply(UINode node) => ViewBindingPlans.Apply(node, this);
    }
}
