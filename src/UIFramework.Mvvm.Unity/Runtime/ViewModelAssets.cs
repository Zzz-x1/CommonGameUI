using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFramework.Mvvm.Unity
{
    public sealed class ViewModelAssets : IViewModelAssets, IViewModelAssetLifecycle, IViewModelAssetPolicy
    {
        private readonly bool _preview;
        public ViewModelAssets(bool preview = false) => _preview = preview;
        public void Initialize(object instance) => ((GlobalViewModel)instance).InitializeRuntime();
        public ValueTask DisposeAsync(object instance) => ((GlobalViewModel)instance).DisposeRuntimeAsync();
        public bool CanCreate(Type type) => typeof(ScriptableObject).IsAssignableFrom(type);
        public object CreateInstance(Type type) => throw new InvalidOperationException("Unity SO VMs must be obtained from a registered GlobalViewModel provider: " + type.FullName);
        public bool IsAsset(object value) => value is ScriptableObject;
        public object Clone(object template)
        {
            if (((GlobalViewModel)template).IsRuntimeInstance) throw new InvalidOperationException("A runtime GlobalViewModel cannot be used as a template.");
            var clone = UnityEngine.Object.Instantiate((GlobalViewModel)template);
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.PrepareRuntime(_preview);
            return clone;
        }
        public void ValidateCreation(Type type, object template, bool shared, bool registeredTemplate)
        {
            if (!typeof(ScriptableObject).IsAssignableFrom(type) && !(template is ScriptableObject)) return;
            if (!shared || !registeredTemplate || !(template is GlobalViewModel vm) || vm.IsRuntimeInstance)
                throw new InvalidOperationException("Unity SO VMs require a registered GlobalViewModel template and shared ownership. Use GetGlobalViewModel/GetViewModel by provider name; use a C# VM for independent state: " + (template?.GetType() ?? type).FullName);
        }
        public void ValidateInstance(object instance, bool shared)
        {
            if (!(instance is ScriptableObject)) return;
            if (!(instance is GlobalViewModel) || !(instance is IMvvmObject) || !shared)
                throw new InvalidOperationException("Unity SO VM must inherit GlobalViewModel, be woven, and use shared ownership: " + instance.GetType().FullName);
        }
        public void ValidateBorrowed(object instance, bool ownedShared)
        {
            if (instance is ScriptableObject && (!(instance is GlobalViewModel) || !ownedShared))
                throw new InvalidOperationException("Only a shared GlobalViewModel instance owned by this Runtime can be borrowed: " + instance.GetType().FullName);
        }
        public void Remap(object template, object instance, Func<object, object> resolveViewModel)
        {
            var visited = new System.Collections.Generic.HashSet<object>(ReferenceComparer.Instance);
            Fields(instance);
            void Fields(object value)
            {
                if (!visited.Add(value)) return;
                for (var type = value.GetType(); type != null && type != typeof(UnityEngine.Object); type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(field => !field.IsInitOnly && !field.IsDefined(typeof(NonSerializedAttribute), true) &&
                            (field.IsPublic || field.IsDefined(typeof(SerializeField), true) || field.IsDefined(typeof(SerializeReference), true))))
                        field.SetValue(value, Map(field.GetValue(value)));
            }
            object Map(object value)
            {
                if (value == null) return null;
                if (value is GlobalViewModel vm) return vm.IsRuntimeInstance ? vm : resolveViewModel(vm);
                if (value is ScriptableObject && value is IMvvmObject)
                    throw new InvalidOperationException("Referenced SO VM must inherit GlobalViewModel: " + value.GetType().FullName);
                if (value is UnityEngine.Object || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return value;
                if (value is System.Collections.IList list)
                {
                    if (!visited.Add(value)) return value;
                    for (var i = 0; i < list.Count; i++) list[i] = Map(list[i]);
                }
                else Fields(value);
                return value;
            }
        }
        private sealed class ReferenceComparer : System.Collections.Generic.IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        public void Release(object instance)
        { if (Application.isPlaying) UnityEngine.Object.Destroy((ScriptableObject)instance); else UnityEngine.Object.DestroyImmediate((ScriptableObject)instance); }
    }
}
