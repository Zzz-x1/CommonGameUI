using System.ComponentModel;

namespace UIFramework.Mvvm;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ViewModelAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldNotifyAttribute : Attribute
{
    public FieldNotifyAttribute(params string[] dependencies) => Dependencies = dependencies;
    public string[] Dependencies { get; }
}
[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute : Attribute { public string? CanExecute { get; set; } }
[AttributeUsage(AttributeTargets.Method)]
public sealed class ConverterAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WovenViewModelAttribute : Attribute { }

/// <summary>Implemented by Cecil; application classes need no framework base class.</summary>
public interface IMvvmObject : INotifyPropertyChanged
{
    object? Read(string member);
    void Write(string member, object? value);
    bool CanExecute(string command);
    Task Execute(string command, object? parameter, CancellationToken token);
    MvvmMember[] Describe();
}
public enum MvvmMemberKind { Property, Command }
public sealed class MvvmMember
{
    public string Name { get; }
    public Type Type { get; }
    public MvvmMemberKind Kind { get; }
    public bool Observable { get; }
    public bool Writable { get; }
    public MvvmMember(string name, Type type, MvvmMemberKind kind, bool observable, bool writable)
    { Name = name; Type = type; Kind = kind; Observable = observable; Writable = writable; }
}

public enum ViewModelCreation { CreateInstance, Manual, SharedAsset }
public enum BindingMode { OneTime, OneWay, OneWayToViewModel, TwoWay }
public sealed class ViewModelSource
{
    public string Name { get; set; } = "";
    public Type Type { get; set; } = typeof(object);
    public ViewModelCreation Creation { get; set; }
    public object? Template { get; set; }
}
public interface IViewModelAssets
{
    bool CanCreate(Type type);
    object CreateInstance(Type type);
    bool IsAsset(object value);
    object Clone(object template);
    void Remap(object template, object instance, Func<object, object> resolveViewModel);
    void Release(object instance);
}
public interface IBindingTarget : IDisposable
{
    Type ValueType { get; }
    object? Read();
    void Write(object? value);
    event Action? Changed;
}
public interface ICommandTarget : IDisposable
{
    event Action? Invoked;
    void SetEnabled(bool enabled);
}
public interface ICollectionTarget : IDisposable
{
    void Insert(int index, object item);
    void Remove(int index);
    void Move(int oldIndex, int newIndex);
    void Clear();
}
public sealed class BindingOptions
{
    public BindingMode Mode { get; set; } = BindingMode.OneWay;
    public Func<object?, object?>? Convert { get; set; }
    public Func<object?, object?>? ConvertBack { get; set; }
    public bool HasNullValue { get; set; }
    public object? NullValue { get; set; }
}
public interface IViewBindingConfiguration
{
    IReadOnlyList<ViewModelSource> Sources { get; }
    void Apply(UINode node);
}
public sealed class BindingValueTarget<T> : IBindingTarget
{
    private readonly Func<T> _read;
    private readonly Action<T> _write;
    private readonly Action _disconnect;
    public BindingValueTarget(Func<T> read, Action<T> write, Action<Action> subscribe, Action disconnect)
    { _read = read; _write = write; _disconnect = disconnect; subscribe(() => Changed?.Invoke()); }
    public Type ValueType => typeof(T);
    public object? Read() => _read();
    public void Write(object? value) => _write((T)value!);
    public event Action? Changed;
    public void Dispose() => _disconnect();
}
