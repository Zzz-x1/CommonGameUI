using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace UIFramework.Mvvm.Generators;

public sealed partial class ControlAdapterGenerator
{
    private static Member ResolveMember(Compilation compilation, Adapter adapter, AttributeData attribute, string key, bool command)
    {
        var control = adapter.Control;
        var declaration = adapter.Declaration;
        var factory = Option(attribute, "Factory");
        if (command)
        {
            var contract = compilation.GetTypeByMetadataName(Api + "ICommandTarget");
            var method = StaticMethod(compilation, adapter, factory, control);
            if (method == null || contract == null || !compilation.ClassifyCommonConversion(method.ReturnType, contract).IsImplicit)
                throw InvalidMember(adapter, key, $"Factory must be an accessible static ICommandTarget {factory}({control.Name}).");
            return new Member(key, Name(declaration) + ".@" + method.Name);
        }
        string? read = Option(attribute, "Read"), write = Option(attribute, "Write"), change = Option(attribute, "Changed");
        string getter;
        ITypeSymbol valueType;
        var readMethod = StaticMethod(compilation, adapter, read, control);
        if (readMethod != null && !readMethod.ReturnsVoid)
        { valueType = readMethod.ReturnType; getter = Name(declaration) + ".@" + read + "(t)"; }
        else
        {
            var candidates = InstanceMembers(compilation, control, read ?? key).ToArray();
            var property = candidates.OfType<IPropertySymbol>().FirstOrDefault(p => Accessible(compilation, p.GetMethod) && !p.IsIndexer);
            var method = candidates.OfType<IMethodSymbol>().FirstOrDefault(m => !m.ReturnsVoid && m.Arity == 0 && m.Parameters.Length == 0);
            var field = candidates.OfType<IFieldSymbol>().FirstOrDefault();
            if (property != null) { valueType = property.Type; getter = "t.@" + property.Name; }
            else if (method != null) { valueType = method.ReturnType; getter = "t.@" + method.Name + "()"; }
            else if (field != null) { valueType = field.Type; getter = "t.@" + field.Name; }
            else throw InvalidMember(adapter, key, $"Cannot read '{read ?? key}'.");
        }
        string setter;
        if (StaticMethod(compilation, adapter, write, control, valueType) is { ReturnsVoid: true })
            setter = Name(declaration) + ".@" + write + "(t, v)";
        else
        {
            var candidates = InstanceMembers(compilation, control, write ?? key).ToArray();
            var property = candidates.OfType<IPropertySymbol>().FirstOrDefault(p => Accessible(compilation, p.SetMethod) &&
                p.SetMethod!.IsInitOnly == false && !p.IsIndexer && CanPass(compilation, valueType, p.Type));
            var method = candidates.OfType<IMethodSymbol>().FirstOrDefault(m => m.ReturnsVoid && m.Arity == 0 &&
                m.Parameters.Length == 1 && m.Parameters[0].RefKind == RefKind.None && CanPass(compilation, valueType, m.Parameters[0].Type));
            var field = candidates.OfType<IFieldSymbol>().FirstOrDefault(f => !f.IsReadOnly && CanPass(compilation, valueType, f.Type));
            if (property != null) setter = "t.@" + property.Name + " = v";
            else if (method != null) setter = "t.@" + method.Name + "(v)";
            else if (field != null) setter = "t.@" + field.Name + " = v";
            else throw InvalidMember(adapter, key, $"Cannot write a {valueType} to '{write ?? key}'.");
        }
        var subscribe = change == null ? "" : ", " + Subscription(compilation, adapter, key, change);
        return new Member(key, $"{Qualified}BindingTargetDefinition.Create<{Name(control)}, {Name(valueType)}>" +
            $"({Literal(key)}, t => {getter}, (t, v) => {setter}{subscribe})", valueType, change != null);
    }

    private static string Subscription(Compilation compilation, Adapter adapter, string key, string change)
    {
        var action = compilation.GetTypeByMetadataName("System.Action")!;
        var method = StaticMethod(compilation, adapter, change, adapter.Control, action);
        if (method != null && Same(method.ReturnType, action)) return Name(adapter.Declaration) + ".@" + change;
        foreach (var member in InstanceMembers(compilation, adapter.Control, change))
        {
            if (member is IEventSymbol ev && ev.Type is INamedTypeSymbol eventType &&
                Accessible(compilation, ev.AddMethod) && Accessible(compilation, ev.RemoveMethod))
            {
                string test = "";
                if (Inherits(adapter.Control, "Godot.GodotObject")) test = "if (global::Godot.GodotObject.IsInstanceValid(t)) ";
                else if (Inherits(adapter.Control, "UnityEngine.Object")) test = "if (t) ";
                return $"(t, changed) => {{ {Listener(adapter, key, eventType)} t.@{change} += listener; " +
                       $"return () => {{ {test}t.@{change} -= listener; }}; }}";
            }
            var objectType = member is IPropertySymbol p && Accessible(compilation, p.GetMethod) ? p.Type : (member as IFieldSymbol)?.Type;
            if (objectType is not INamedTypeSymbol signal) continue;
            var removes = InstanceMembers(compilation, signal, "RemoveListener").OfType<IMethodSymbol>().ToArray();
            foreach (var add in InstanceMembers(compilation, signal, "AddListener").OfType<IMethodSymbol>())
            {
                if (add.Parameters.Length != 1 || !(add.Parameters[0].Type is INamedTypeSymbol handler) ||
                    !removes.Any(remove => remove.Parameters.Length == 1 && Same(remove.Parameters[0].Type, handler))) continue;
                // Capture the event object: the component may replace its event before unbinding.
                return $"(t, changed) => {{ var signal = t.@{change}; {Listener(adapter, key, handler)} " +
                    "signal.AddListener(listener); return () => signal.RemoveListener(listener); }";
            }
        }
        throw InvalidMember(adapter, key, $"'{change}' must be an accessible event, listener pair or static Action subscribe(control, Action).");
    }
    private static string Listener(Adapter adapter, string key, INamedTypeSymbol type)
    {
        var invoke = type.DelegateInvokeMethod;
        if (invoke == null || !invoke.ReturnsVoid || invoke.Parameters.Any(p => p.RefKind != RefKind.None))
            throw InvalidMember(adapter, key, $"Unsupported change delegate {type}.");
        var parameters = string.Join(", ", Enumerable.Range(0, invoke.Parameters.Length).Select(i => "arg" + i));
        return $"{Name(type)} listener = ({parameters}) => changed();";
    }
    private static bool Inherits(INamedTypeSymbol? type, string name)
    {
        for (; type != null; type = type.BaseType)
            if (type.ToDisplayString() == name) return true;
        return false;
    }
    private static bool CanPass(Compilation compilation, ITypeSymbol valueType, ITypeSymbol destination) =>
        compilation.ClassifyCommonConversion(valueType, destination).IsImplicit;
    private static bool Accessible(Compilation compilation, ISymbol? member) =>
        member != null && compilation.IsSymbolAccessibleWithin(member, compilation.Assembly);
    private static IEnumerable<ISymbol> InstanceMembers(Compilation compilation, INamedTypeSymbol? type, string member)
    {
        for (; type != null; type = type.BaseType)
        {
            var candidates = type.GetMembers(member);
            if (candidates.Length == 0) continue;
            return candidates.Where(x => !x.IsStatic && Accessible(compilation, x));
        }
        return Enumerable.Empty<ISymbol>();
    }
    private static IMethodSymbol? StaticMethod(Compilation compilation, Adapter adapter, string? name, params ITypeSymbol[] args)
    {
        if (name == null) return null;
        return adapter.Declaration.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(method =>
            method.IsStatic && method.Arity == 0 && Accessible(compilation, method) && method.Parameters.Length == args.Length &&
            method.Parameters.Select((parameter, i) => parameter.RefKind == RefKind.None && Same(parameter.Type, args[i])).All(match => match));
    }
    private static InvalidOperationException InvalidMember(Adapter adapter, string key, string cause) =>
        new($"{adapter.Declaration} binding {adapter.Control}.{key}: {cause}");
}
