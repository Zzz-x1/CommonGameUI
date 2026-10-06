using Godot;
using System.Reflection;
namespace UIFramework.Mvvm.Godot;
public sealed class ViewModelAssets : IViewModelAssets
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Resource, List<Action>> Containers = new();
    public bool CanCreate(Type type) => typeof(Resource).IsAssignableFrom(type);
    public object CreateInstance(Type type) => Activator.CreateInstance(type)!;
    public bool IsAsset(object value) => value is Resource && value is IMvvmObject;
    public object Clone(object template) => ((Resource)template).Duplicate(false);
    public void Remap(object template, object instance, Func<object, object> resolveViewModel)
    {
        var source = (Resource)template; var clone = (Resource)instance; var owned = Containers.GetOrCreateValue(clone);
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
            if (value.VariantType == Variant.Type.Array) { var copy = value.AsGodotArray().Duplicate(false); owned.Add(() => { copy.Clear(); copy.Dispose(); }); for (int i = 0; i < copy.Count; i++) copy[i] = Copy(copy[i]); return Variant.From(copy); }
            if (value.VariantType == Variant.Type.Dictionary) { var copy = value.AsGodotDictionary().Duplicate(false); owned.Add(() => { copy.Clear(); copy.Dispose(); }); foreach (var key in copy.Keys.ToArray()) copy[key] = Copy(copy[key]); return Variant.From(copy); }
            return value;
        }
    }
    public void Release(object instance)
    {
        var resource = (Resource)instance; Exception? first = null;
        if (Containers.TryGetValue(resource, out var containers))
        {
            foreach (var release in containers.AsEnumerable().Reverse()) Attempt(release);
            Containers.Remove(resource);
        }
        Attempt(resource.Dispose);
        if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        void Attempt(Action release)
        {
            try { release(); } catch (Exception error) {
                if (first == null) first = error;
                else { if (first.Data[UIFailure.SecondaryExceptionsKey] is not List<Exception> errors) first.Data[UIFailure.SecondaryExceptionsKey] = errors = new(); errors.Add(error); }
            }
        }
    }
}
