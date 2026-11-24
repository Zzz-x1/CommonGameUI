using System.ComponentModel;

namespace UIFramework.Mvvm;

/// <summary>Declares the primary Bindable() surface of a native control.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ControlAdapterAttribute(Type control) : Attribute
{
    public Type Control { get; } = control;
}

/// <summary>Adds extension members to an existing control adapter.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ExtendControlAdapterAttribute(Type control) : Attribute
{
    public Type Control { get; } = control;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class BindPropertyAttribute(string member) : Attribute
{
    public string Member { get; } = member;
    public string? Read { get; set; }
    public string? Write { get; set; }
    public string? Changed { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class BindCommandAttribute(string member) : Attribute
{
    public string Member { get; } = member;
    public string? Factory { get; set; }
}

/// <summary>Emitted by the generator so other assemblies can extend this control.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ControlAdapterMapAttribute(Type control, Type wrapper) : Attribute
{
    public Type Control { get; } = control;
    public Type Wrapper { get; } = wrapper;
}

/// <summary>Identifies a declared member when compiling an extension in another assembly.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ControlAdapterMemberAttribute(Type control, string member) : Attribute
{
    public Type Control { get; } = control;
    public string Member { get; } = member;
}

/// <summary>Access to the native control for generated properties and extensions.</summary>
public abstract class BindableMembers(object control)
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object Control { get; } = control ?? throw new ArgumentNullException(nameof(control));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public IBindingTarget Property(string member) =>
        BindingTargetRegistry.Find(Control.GetType(), member).CreateTarget(Control);
}
