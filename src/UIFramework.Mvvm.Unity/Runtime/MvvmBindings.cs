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
    { public string Name = ""; public ViewModelCreation Creation; public ScriptableObject Template; }
    [Serializable] public sealed class ViewBindingData
    {
        public bool Enabled = true; public Component Target; public string Member = "text"; public string Source = "";
        public BindingMode Mode = BindingMode.OneWay; public ViewBindingKind Kind;
        public string Converter = "", ConvertBack = ""; public ConverterInput ConverterInput; public string ConverterConstant = ""; public bool HasNullValue; public string NullValue = "";
        public GameObject ItemPrefab; public bool OneTimeCollection;
        public CommandParameterMode ParameterMode; public string ParameterConstant = "", ParameterPath = "";
        public CommandBindingOptions GetCommandOptions() => ParameterMode == CommandParameterMode.SourcePath
            ? new CommandBindingOptions { ParameterPath = ParameterPath }
            : new CommandBindingOptions { Mode = ParameterMode, ConstantText = ParameterMode == CommandParameterMode.Constant ? ParameterConstant : null };
        public BindingOptions GetOptions(bool preview = false)
        {
            var options = Converters.Configure(Converter, ConvertBack, Mode, ConverterInput, ConverterConstant, HasNullValue, NullValue);
            if (preview) { options.Mode = Mode == BindingMode.OneTime ? BindingMode.OneTime : BindingMode.OneWay; options.ConvertBack = null; }
            return options;
        }
    }
    public sealed class MvvmBindings : MonoBehaviour, IViewBindingConfiguration
    {
        public List<ViewModelSourceData> ViewModels = new List<ViewModelSourceData>();
        public List<ViewBindingData> Bindings = new List<ViewBindingData>();
        public IReadOnlyList<ViewModelSource> Sources => ViewModels.Select(s => new ViewModelSource
        { Name = s.Name, Type = s.Template ? s.Template.GetType() : throw new InvalidOperationException("VM asset is required: " + s.Name), Creation = s.Creation, Template = s.Template }).ToArray();
        public void Apply(UINode node)
        {
            foreach (var binding in Bindings.Where(b => b.Enabled))
            {
                if (!binding.Target) throw new InvalidOperationException("Missing binding target: " + binding.Source);
                if (binding.Kind == ViewBindingKind.Command) node.BindCommand(binding.Source, ((Button)binding.Target).ClickTarget(), binding.GetCommandOptions());
                else if (binding.Kind == ViewBindingKind.Collection) node.BindCollection(binding.Source, new CollectionTarget(node, (RectTransform)binding.Target.transform, binding.ItemPrefab), binding.OneTimeCollection);
                else { var options = binding.GetOptions(); node.Bind(binding.Source, BindingTargets.Property(binding.Target, binding.Member, options.Mode), options); }
            }
        }
    }
    public static class MvvmExtensions
    {
        public static T GetSharedViewModel<T>(this UIContext ui, T template) where T : ScriptableObject => ui.GetMvvm().GetSharedViewModel(template, new ViewModelAssets());
    public static T GetSharedViewModel<T>(this MvvmRuntime runtime, T template) where T : ScriptableObject => runtime.GetSharedViewModel(template, new ViewModelAssets());

    }
}
