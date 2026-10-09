using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UIFramework.Mvvm.Unity
{
    [UnityEngine.Scripting.Preserve]
    public sealed class GlobalViewModelProvider : IGlobalViewModelProvider
    {
        public const string ResourcePath = "UIFramework/Mvvm";
        public IReadOnlyDictionary<string, object> ViewModels { get; }
        public IViewModelAssets Assets { get; }
        public GlobalViewModelProvider(IEnumerable<GlobalViewModel> viewModels, bool preview = false)
        {
            Assets = new ViewModelAssets(preview);
            var definitions = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var asset in viewModels)
            {
                if (!asset) throw new ArgumentException("Global VM catalog contains a null asset.");
                if (asset.IsRuntimeInstance) throw new ArgumentException("Global VM provider requires template assets: " + asset.name);
                if (definitions.ContainsKey(asset.name)) throw new ArgumentException("Duplicate global VM name '" + asset.name + "': " + asset);
                definitions.Add(asset.name, asset);
            }
            ViewModels = definitions;
        }
        public static GlobalViewModelProvider Load(bool preview = false) => new GlobalViewModelProvider(
            Resources.LoadAll<GlobalViewModelCatalog>(ResourcePath).SelectMany(catalog => catalog.ViewModels), preview);
    }
}
