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
        public IReadOnlyList<object> ViewModels { get; }
        public IViewModelAssets Assets { get; } = new ViewModelAssets();
        public GlobalViewModelProvider(IEnumerable<ScriptableObject> viewModels)
        { ViewModels = viewModels.Cast<object>().ToArray(); }
        public static GlobalViewModelProvider Load() => new GlobalViewModelProvider(
            Resources.LoadAll<GlobalViewModelCatalog>(ResourcePath).SelectMany(catalog => catalog.ViewModels));
    }
}
