using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UIFramework.Mvvm.Weaver;

public sealed class ViewModelWeaver
{
    private readonly ModuleDefinition _module;
    private readonly ModuleDefinition _runtime;
    private const string Prefix = "UIFramework.Mvvm.";
    public ViewModelWeaver(ModuleDefinition module, ModuleDefinition runtime) { _module = module; _runtime = runtime; }
    private MethodReference Runtime(string type, string method) => _module.ImportReference(_runtime.GetType(Prefix + type).Methods.Single(m => m.Name == method));
    private TypeReference RuntimeType(string name) => _module.ImportReference(_runtime.GetType(Prefix + name));
    private static bool Attribute(ICustomAttributeProvider member, string name) => member.CustomAttributes.Any(a => a.AttributeType.FullName == Prefix + name);
    public int Process()
    {
        int count = 0;
        foreach (var type in Types(_module.Types).Where(t => Attribute(t, "ViewModelAttribute")).ToArray()) if (Weave(type)) count++;
        if (WeaveConverters()) count++;
        return count;
    }
    private bool WeaveConverters()
    {
        if (_module.Assembly.CustomAttributes.Any(a => a.AttributeType.FullName == Prefix + "ConverterProviderAttribute")) return false;
        var methods = Types(_module.Types).SelectMany(t => t.Methods).Where(m => Attribute(m, "ConverterAttribute")).ToArray();
        if (methods.Length == 0) return false;
        var provider = new TypeDefinition("UIFramework.Mvvm.Generated", "ConverterProvider", TypeAttributes.Public | TypeAttributes.Class, _module.TypeSystem.Object);
        _module.Types.Add(provider); provider.Interfaces.Add(new InterfaceImplementation(RuntimeType("IConverterProvider")));
        var constructor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, _module.TypeSystem.Void);
        provider.Methods.Add(constructor); var ci = constructor.Body.GetILProcessor(); ci.Emit(OpCodes.Ldarg_0);
        ci.Emit(OpCodes.Call, new MethodReference(".ctor", _module.TypeSystem.Void, _module.TypeSystem.Object) { HasThis = true }); ci.Emit(OpCodes.Ret);
        var signature = _runtime.GetType(Prefix + "IConverterProvider").Methods.Single();
        var get = new MethodDefinition("GetConverters", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig, _module.ImportReference(signature.ReturnType)); provider.Methods.Add(get);
        var il = get.Body.GetILProcessor(); var info = RuntimeType("ConverterInfo"); il.Emit(OpCodes.Ldc_I4, methods.Length); il.Emit(OpCodes.Newarr, info);
        var infoCtor = Runtime("ConverterInfo", ".ctor"); var func = infoCtor.Parameters[3].ParameterType;
        for (int index = 0; index < methods.Length; index++)
        {
            var method = methods[index];
            if (!method.IsPublic || !method.IsStatic || method.HasGenericParameters || method.Parameters.Count != 1 || method.ReturnType.FullName == "System.Void") throw new InvalidOperationException("Converter must be public static TOut Convert(TIn): " + method.FullName);
            var call = new MethodDefinition("Convert_" + index, MethodAttributes.Private | MethodAttributes.Static, _module.TypeSystem.Object); call.Parameters.Add(new ParameterDefinition(_module.TypeSystem.Object)); provider.Methods.Add(call);
            var body = call.Body.GetILProcessor(); body.Emit(OpCodes.Ldarg_0); Unbox(body, method.Parameters[0].ParameterType); body.Emit(OpCodes.Call, method); Box(body, method.ReturnType); body.Emit(OpCodes.Ret);
            il.Emit(OpCodes.Dup); il.Emit(OpCodes.Ldc_I4, index); il.Emit(OpCodes.Ldstr, method.DeclaringType.FullName + "." + method.Name);
            var typeClass = new TypeReference("System", "Type", _module, _module.TypeSystem.CoreLibrary);
            var handle = new TypeReference("System", "RuntimeTypeHandle", _module, _module.TypeSystem.CoreLibrary, true);
            var getType = new MethodReference("GetTypeFromHandle", typeClass, typeClass) { HasThis = false }; getType.Parameters.Add(new ParameterDefinition(handle));
            il.Emit(OpCodes.Ldtoken, _module.ImportReference(method.Parameters[0].ParameterType)); il.Emit(OpCodes.Call, getType);
            il.Emit(OpCodes.Ldtoken, _module.ImportReference(method.ReturnType)); il.Emit(OpCodes.Call, getType);
            il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Ldftn, call);
            var functionCtor = new MethodReference(".ctor", _module.TypeSystem.Void, func) { HasThis = true }; functionCtor.Parameters.Add(new ParameterDefinition(_module.TypeSystem.Object)); functionCtor.Parameters.Add(new ParameterDefinition(_module.TypeSystem.IntPtr));
            il.Emit(OpCodes.Newobj, functionCtor); il.Emit(OpCodes.Newobj, infoCtor); il.Emit(OpCodes.Stelem_Ref);
        }
        il.Emit(OpCodes.Ret);
        var attribute = new CustomAttribute(Runtime("ConverterProviderAttribute", ".ctor")); attribute.ConstructorArguments.Add(new CustomAttributeArgument(_module.ImportReference(_runtime.GetType(Prefix + "ConverterProviderAttribute").Methods.Single(m => m.Name == ".ctor").Parameters[0].ParameterType), provider)); _module.Assembly.CustomAttributes.Add(attribute);
        return true;
    }
    private static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
    { foreach (var type in types) { yield return type; foreach (var nested in Types(type.NestedTypes)) yield return nested; } }
    private bool Weave(TypeDefinition type)
    {
        if (Attribute(type, "WovenViewModelAttribute")) return false;
        if (!type.IsClass || type.IsAbstract || type.HasGenericParameters) throw new InvalidOperationException("VM must be a concrete non-generic class: " + type.FullName);
        TypeDefinition? parent = type.BaseType?.Resolve();
        bool inherited = parent != null && Attribute(parent, "ViewModelAttribute");
        if (inherited && parent!.Module == _module) Weave(parent);
        var properties = type.Properties.Where(p => p.GetMethod?.IsPublic == true && !p.GetMethod.IsStatic && !p.HasParameters).ToList();
        var commands = type.Methods.Where(m => Attribute(m, "CommandAttribute")).ToList();
        if (inherited)
        {
            for (var baseType = parent; baseType != null && Attribute(baseType, "ViewModelAttribute"); baseType = baseType.BaseType?.Resolve())
            {
                properties.AddRange(baseType.Properties.Where(p => p.GetMethod?.IsPublic == true && !p.HasParameters && properties.All(x => x.Name != p.Name)));
                commands.AddRange(baseType.Methods.Where(m => Attribute(m, "CommandAttribute") && commands.All(x => x.Name != m.Name)));
            }
        }
        FieldReference events;
        if (inherited)
        {
            var declaring = parent!;
            while (!declaring.Fields.Any(f => f.Name == "__mvvmChanged")) declaring = declaring.BaseType.Resolve();
            events = _module.ImportReference(declaring.Fields.Single(f => f.Name == "__mvvmChanged"));
        }
        else
        {
            var eventType = _module.ImportReference(_runtime.GetType(Prefix + "MvvmNotifications").Methods.Single(m => m.Name == "Raise").Parameters[0].ParameterType);
            var field = new FieldDefinition("__mvvmChanged", FieldAttributes.Family | FieldAttributes.NotSerialized, eventType); type.Fields.Add(field); events = field;
            type.Interfaces.Add(new InterfaceImplementation(RuntimeType("IMvvmObject")));
            Event(type, field, true); Event(type, field, false);
        }
        foreach (var property in type.Properties.Where(p => Attribute(p, "FieldNotifyAttribute")))
        {
            if (property.GetMethod == null || property.GetMethod.IsStatic || property.HasParameters) throw new InvalidOperationException("Invalid FieldNotify property: " + property.FullName);
            if (property.SetMethod != null) Setter(type, property, events, Dependencies(properties, property.Name));
        }
        ValidateDependencies(properties);
        Notify(type, properties, events, inherited);
        Dispatch(type, "Read", properties, commands, inherited);
        Dispatch(type, "Write", properties, commands, inherited);
        Dispatch(type, "CanExecute", properties, commands, inherited);
        Dispatch(type, "Execute", properties, commands, inherited);
        Describe(type, properties, commands, inherited);
        type.CustomAttributes.Add(new CustomAttribute(Runtime("WovenViewModelAttribute", ".ctor")));
        return true;
    }
    private static string[] DeclaredDependencies(PropertyDefinition property)
    {
        var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == Prefix + "FieldNotifyAttribute");
        return attribute == null ? Array.Empty<string>() : ((CustomAttributeArgument[])attribute.ConstructorArguments[0].Value).Select(a => (string)a.Value).ToArray();
    }
    private void Describe(TypeDefinition type, List<PropertyDefinition> properties, List<MethodDefinition> commands, bool inherited)
    {
        var memberType = RuntimeType("MvvmMember");
        var method = new MethodDefinition("Describe", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | (inherited ? 0 : MethodAttributes.NewSlot), new ArrayType(memberType)); type.Methods.Add(method);
        var il = method.Body.GetILProcessor(); il.Emit(OpCodes.Ldc_I4, properties.Count + commands.Count); il.Emit(OpCodes.Newarr, memberType);
        var typeClass = new TypeReference("System", "Type", _module, _module.TypeSystem.CoreLibrary);
        var getType = new MethodReference("GetTypeFromHandle", typeClass, typeClass) { HasThis = false };
        getType.Parameters.Add(new ParameterDefinition(new TypeReference("System", "RuntimeTypeHandle", _module, _module.TypeSystem.CoreLibrary, true)));
        int index = 0;
        foreach (var property in properties) Emit(property.Name, property.PropertyType, 0, Attribute(property, "FieldNotifyAttribute"), property.SetMethod?.IsPublic == true);
        foreach (var command in commands) Emit(command.Name, command.Parameters.FirstOrDefault(p => p.ParameterType.FullName != "System.Threading.CancellationToken")?.ParameterType ?? _module.TypeSystem.Void, 1, false, false);
        il.Emit(OpCodes.Ret);
        void Emit(string name, TypeReference valueType, int kind, bool observable, bool writable)
        {
            il.Emit(OpCodes.Dup); il.Emit(OpCodes.Ldc_I4, index++); il.Emit(OpCodes.Ldstr, name);
            il.Emit(OpCodes.Ldtoken, _module.ImportReference(valueType)); il.Emit(OpCodes.Call, getType);
            il.Emit(OpCodes.Ldc_I4, kind); il.Emit(OpCodes.Ldc_I4, observable ? 1 : 0); il.Emit(OpCodes.Ldc_I4, writable ? 1 : 0);
            il.Emit(OpCodes.Newobj, Runtime("MvvmMember", ".ctor")); il.Emit(OpCodes.Stelem_Ref);
        }
    }
    private static void ValidateDependencies(List<PropertyDefinition> properties)
    {
        foreach (var property in properties)
            foreach (var name in DeclaredDependencies(property))
                if (!properties.Any(p => p.Name == name && Attribute(p, "FieldNotifyAttribute"))) throw new InvalidOperationException("Unknown/non-notifying dependency: " + property.FullName + " -> " + name);
        foreach (var property in properties) Visit(property.Name, new HashSet<string>());
        void Visit(string name, HashSet<string> path)
        {
            if (!path.Add(name)) throw new InvalidOperationException("FieldNotify dependency cycle: " + name);
            foreach (var child in properties.Where(p => DeclaredDependencies(p).Contains(name))) Visit(child.Name, new HashSet<string>(path));
        }
    }
    private static string[] Dependencies(List<PropertyDefinition> properties, string name)
    {
        var found = new HashSet<string> { name }; var queue = new Queue<string>(); queue.Enqueue(name);
        while (queue.Count != 0)
        {
            var dependency = queue.Dequeue();
            foreach (var child in properties.Where(p => DeclaredDependencies(p).Contains(dependency))) if (found.Add(child.Name)) queue.Enqueue(child.Name);
        }
        return found.ToArray();
    }
    private void Event(TypeDefinition type, FieldDefinition field, bool add)
    {
        var method = new MethodDefinition((add ? "add_" : "remove_") + "PropertyChanged", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName, _module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, field.FieldType)); type.Methods.Add(method);
        var il = method.Body.GetILProcessor(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ldarg_1);
        var delegateType = new TypeReference("System", "Delegate", _module, _module.TypeSystem.CoreLibrary);
        var combine = new MethodReference(add ? "Combine" : "Remove", delegateType, delegateType) { HasThis = false };
        combine.Parameters.Add(new ParameterDefinition(delegateType)); combine.Parameters.Add(new ParameterDefinition(delegateType));
        il.Emit(OpCodes.Call, combine); il.Emit(OpCodes.Castclass, field.FieldType); il.Emit(OpCodes.Stfld, field); il.Emit(OpCodes.Ret);
        var eventDefinition = type.Events.FirstOrDefault(e => e.Name == "PropertyChanged");
        if (eventDefinition == null) { eventDefinition = new EventDefinition("PropertyChanged", EventAttributes.None, field.FieldType); type.Events.Add(eventDefinition); }
        if (add) eventDefinition.AddMethod = method; else eventDefinition.RemoveMethod = method;
    }
    private void Setter(TypeDefinition type, PropertyDefinition property, FieldReference events, string[] notifications)
    {
        var setter = property.SetMethod;
        if (!setter.HasBody || setter.IsStatic || setter.Parameters.Count != 1) throw new InvalidOperationException("Unsupported setter: " + setter.FullName);
        var original = new MethodDefinition("__mvvmOriginal_" + setter.Name, MethodAttributes.Private | MethodAttributes.HideBySig, _module.TypeSystem.Void);
        original.Parameters.Add(new ParameterDefinition(setter.Parameters[0].Name, setter.Parameters[0].Attributes, setter.Parameters[0].ParameterType)); original.Body = setter.Body;
        foreach (var instruction in original.Body.Instructions) if (instruction.Operand == setter.Parameters[0]) instruction.Operand = original.Parameters[0];
        foreach (var point in setter.DebugInformation.SequencePoints) original.DebugInformation.SequencePoints.Add(point);
        setter.DebugInformation.SequencePoints.Clear(); original.DebugInformation.Scope = setter.DebugInformation.Scope; setter.DebugInformation.Scope = null;
        type.Methods.Add(original); setter.Body = new MethodBody(setter) { InitLocals = true };
        var previous = new VariableDefinition(_module.ImportReference(property.PropertyType)); setter.Body.Variables.Add(previous);
        var il = setter.Body.GetILProcessor(); var done = Instruction.Create(OpCodes.Ret);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, _module.ImportReference(property.GetMethod)); il.Emit(OpCodes.Stloc, previous);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, original);
        il.Emit(OpCodes.Ldloc, previous); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, _module.ImportReference(property.GetMethod));
        var different = new GenericInstanceMethod(Runtime("MvvmNotifications", "Different")); different.GenericArguments.Add(_module.ImportReference(property.PropertyType));
        il.Emit(OpCodes.Call, different); il.Emit(OpCodes.Brfalse, done);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldstr, property.Name);
        il.Emit(OpCodes.Callvirt, new MethodReference("__mvvmNotify", _module.TypeSystem.Void, type) { HasThis = true, Parameters = { new ParameterDefinition(_module.TypeSystem.String) } });
        il.Append(done);
    }
    private void Notify(TypeDefinition type, List<PropertyDefinition> properties, FieldReference events, bool inherited)
    {
        var method = new MethodDefinition("__mvvmNotify", MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig | (inherited ? 0 : MethodAttributes.NewSlot), _module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition(_module.TypeSystem.String)); type.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        foreach (var property in properties.Where(p => Attribute(p, "FieldNotifyAttribute")))
        {
            var next = Compare(il, property.Name);
            foreach (var name in Dependencies(properties, property.Name))
            { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, events); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldstr, name); il.Emit(OpCodes.Call, Runtime("MvvmNotifications", "Raise")); }
            il.Emit(OpCodes.Ret); il.Append(next);
        }
        il.Emit(OpCodes.Ret);
    }
    private void Dispatch(TypeDefinition type, string name, List<PropertyDefinition> properties, List<MethodDefinition> commands, bool inherited)
    {
        var signature = _runtime.GetType(Prefix + "IMvvmObject").Methods.Single(m => m.Name == name);
        var method = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | (inherited ? 0 : MethodAttributes.NewSlot), _module.ImportReference(signature.ReturnType));
        foreach (var parameter in signature.Parameters) method.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, _module.ImportReference(parameter.ParameterType)));
        type.Methods.Add(method); var il = method.Body.GetILProcessor();
        if (name is "Read" or "Write")
        {
            foreach (var property in properties.Where(p => name == "Read" || p.SetMethod?.IsPublic == true))
            {
                var next = Compare(il, property.Name);
                il.Emit(OpCodes.Ldarg_0);
                if (name == "Write") { il.Emit(OpCodes.Ldarg_2); Unbox(il, property.PropertyType); }
                il.Emit(OpCodes.Callvirt, _module.ImportReference(name == "Read" ? property.GetMethod : property.SetMethod));
                if (name == "Read") Box(il, property.PropertyType);
                il.Emit(OpCodes.Ret); il.Append(next);
            }
        }
        else foreach (var command in commands)
        {
            var attribute = command.CustomAttributes.Single(a => a.AttributeType.FullName == Prefix + "CommandAttribute");
            if (!command.IsPublic || command.IsStatic || command.HasGenericParameters || (command.ReturnType.FullName != "System.Void" && command.ReturnType.FullName != "System.Threading.Tasks.Task")) throw new InvalidOperationException("Unsupported command: " + command.FullName);
            if (command.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.AsyncStateMachineAttribute") && command.ReturnType.FullName == "System.Void") throw new InvalidOperationException("async void commands are forbidden.");
            if (command.Parameters.Count(p => p.ParameterType.FullName != "System.Threading.CancellationToken") > 1) throw new InvalidOperationException("Command takes at most one business parameter.");
            var next = Compare(il, command.Name);
            if (name == "CanExecute")
            {
                var condition = attribute.Properties.FirstOrDefault(p => p.Name == "CanExecute").Argument.Value as string;
                if (condition == null) il.Emit(OpCodes.Ldc_I4_1);
                else { var property = properties.Single(p => p.Name == condition && p.PropertyType.FullName == "System.Boolean"); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, _module.ImportReference(property.GetMethod)); }
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                foreach (var parameter in command.Parameters)
                    if (parameter.ParameterType.FullName == "System.Threading.CancellationToken") il.Emit(OpCodes.Ldarg_3);
                    else { il.Emit(OpCodes.Ldarg_2); Unbox(il, parameter.ParameterType); }
                il.Emit(OpCodes.Callvirt, _module.ImportReference(command));
                if (command.ReturnType.FullName == "System.Void") il.Emit(OpCodes.Call, Runtime("CommandTasks", "get_Completed"));
            }
            il.Emit(OpCodes.Ret); il.Append(next);
        }
        il.Emit(OpCodes.Ldarg_1);
        var errorType = new TypeReference("System.Collections.Generic", "KeyNotFoundException", _module, _module.TypeSystem.CoreLibrary);
        var constructor = new MethodReference(".ctor", _module.TypeSystem.Void, errorType) { HasThis = true }; constructor.Parameters.Add(new ParameterDefinition(_module.TypeSystem.String));
        il.Emit(OpCodes.Newobj, constructor); il.Emit(OpCodes.Throw);
    }
    private Instruction Compare(ILProcessor il, string name)
    {
        var next = Instruction.Create(OpCodes.Nop); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldstr, name);
        var equal = new MethodReference("op_Equality", _module.TypeSystem.Boolean, _module.TypeSystem.String) { HasThis = false };
        equal.Parameters.Add(new ParameterDefinition(_module.TypeSystem.String)); equal.Parameters.Add(new ParameterDefinition(_module.TypeSystem.String));
        il.Emit(OpCodes.Call, equal); il.Emit(OpCodes.Brfalse, next); return next;
    }
    private void Box(ILProcessor il, TypeReference type) { if (type.IsValueType || type.IsGenericParameter) il.Emit(OpCodes.Box, _module.ImportReference(type)); }
    private void Unbox(ILProcessor il, TypeReference type) => il.Emit(type.IsValueType || type.IsGenericParameter ? OpCodes.Unbox_Any : OpCodes.Castclass, _module.ImportReference(type));
}
