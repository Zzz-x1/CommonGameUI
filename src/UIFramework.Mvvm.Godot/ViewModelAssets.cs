using Godot;
using System.Reflection;
namespace UIFramework.Mvvm.Godot;
public sealed class ViewModelAssets : IViewModelAssets
{
    public bool CanCreate(Type type) => typeof(Resource).IsAssignableFrom(type);
    public object CreateInstance(Type type) => Activator.CreateInstance(type)!;
    public bool IsAsset(object value) => value is Resource && value is IMvvmObject;
    public object Clone(object template) => ((Resource)template).Duplicate(false);
    public void Remap(object template, object instance, Func<object, object> resolveViewModel)
    {
        var source = (Resource)template; var clone = (Resource)instance;
        foreach (var property in source.GetPropertyList())
        {
            if (((PropertyUsageFlags)property["usage"].AsInt64() & PropertyUsageFlags.Storage) == 0) continue;
            var name = property["name"].AsStringName(); var value = source.Get(name);
            if (value.VariantType is Variant.Type.Array or Variant.Type.Dictionary || value.VariantType == Variant.Type.Object && value.AsGodotObject() is IMvvmObject)
                clone.Set(name, Copy(value));
        }
        Variant Copy(Variant value)
        {
            if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is Resource resource && resource is IMvvmObject) return Variant.From((Resource)resolveViewModel(resource));
            if (value.VariantType == Variant.Type.Array) { var copy = value.AsGodotArray().Duplicate(false); for (int i = 0; i < copy.Count; i++) copy[i] = Copy(copy[i]); return Variant.From(copy); }
            if (value.VariantType == Variant.Type.Dictionary) { var copy = value.AsGodotDictionary().Duplicate(false); foreach (var key in copy.Keys.ToArray()) copy[key] = Copy(copy[key]); return Variant.From(copy); }
            return value;
        }
    }
    public void Release(object instance) => ((Resource)instance).Dispose();
}
