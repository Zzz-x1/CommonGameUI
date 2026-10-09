using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UIFramework.Mvvm.Unity
{
    public sealed class ViewModelAssets : IViewModelAssets, IViewModelInitialization
    {
        private readonly bool _preview;
        public ViewModelAssets(bool preview = false) => _preview = preview;
        public void Initialize(object instance)
        {
            if (!_preview && instance is IViewModelInitialize initialization) initialization.OnInitialize();
        }
        public bool CanCreate(Type type) => typeof(ScriptableObject).IsAssignableFrom(type);
        public object CreateInstance(Type type) { var value = ScriptableObject.CreateInstance(type); value.hideFlags = HideFlags.HideAndDontSave; return value; }
        public bool IsAsset(object value) => value is ScriptableObject && value is IMvvmObject;
        public object Clone(object template) { var clone = UnityEngine.Object.Instantiate((ScriptableObject)template); clone.hideFlags = HideFlags.HideAndDontSave; return clone; }
        public void Remap(object template, object instance, Func<object, object> resolveViewModel)
        {
            var source = (IMvvmObject)template; var clone = (IMvvmObject)instance;
            foreach (var property in source.Describe().Where(p => p.Kind == MvvmMemberKind.Property && p.Writable))
                clone.Write(property.Name, Copy(source.Read(property.Name)));
            object Copy(object value)
            {
                if (value is ScriptableObject && value is IMvvmObject) return resolveViewModel(value);
                if (value is Array array) { var copy = Array.CreateInstance(array.GetType().GetElementType(), array.Length); for (int i = 0; i < array.Length; i++) copy.SetValue(Copy(array.GetValue(i)), i); return copy; }
                if (value is System.Collections.IDictionary dictionary) { var copy = (System.Collections.IDictionary)Activator.CreateInstance(value.GetType()); foreach (System.Collections.DictionaryEntry pair in dictionary) copy.Add(Copy(pair.Key), Copy(pair.Value)); return copy; }
                if (value is System.Collections.IList list) { var copy = (System.Collections.IList)Activator.CreateInstance(value.GetType()); foreach (var item in list) copy.Add(Copy(item)); return copy; }
                return value;
            }
        }
        public void Release(object instance)
        { if (Application.isPlaying) UnityEngine.Object.Destroy((ScriptableObject)instance); else UnityEngine.Object.DestroyImmediate((ScriptableObject)instance); }
    }
}
