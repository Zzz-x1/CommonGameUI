using System.Collections.Generic;
using UnityEngine;

namespace UIFramework.Mvvm.Unity
{
    [CreateAssetMenu(menuName = "UIFramework/MVVM/Global ViewModel Catalog")]
    public sealed class GlobalViewModelCatalog : ScriptableObject
    {
        public List<ScriptableObject> ViewModels = new List<ScriptableObject>();
    }
}
