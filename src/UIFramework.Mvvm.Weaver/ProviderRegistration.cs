using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UIFramework.Mvvm.Weaver;

public sealed partial class ViewModelWeaver
{
    public const string RegistrationType = "UIFramework.Mvvm.Generated.BindingRegistration";
    public MethodDefinition? Registration => _module.GetType(RegistrationType)?.Methods.Single(m => m.Name == "Register");

    private void GenerateRegistration(MethodDefinition startup, IEnumerable<MethodReference> dependencies)
    {
        var calls = dependencies.OrderBy(m => m.DeclaringType.Scope.Name, StringComparer.Ordinal).ToArray();
        var providers = _module.Assembly.CustomAttributes.Where(a => a.AttributeType.FullName is
            Prefix + "ConverterProviderAttribute" or Prefix + "BindingTargetProviderAttribute" or Prefix + "GlobalViewModelProviderAttribute").ToArray();
        var register = Registration;
        if (register == null && calls.Length == 0 && providers.Length == 0) return;
        if (register == null)
        {
            var type = new TypeDefinition("UIFramework.Mvvm.Generated", "BindingRegistration",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, _module.TypeSystem.Object);
            _module.Types.Add(type);
            register = new MethodDefinition("Register", MethodAttributes.Public | MethodAttributes.Static, _module.TypeSystem.Void);
            type.Methods.Add(register);
            var il = register.Body.GetILProcessor();
            var registered = new FieldDefinition("registered", FieldAttributes.Static | FieldAttributes.Private, _module.TypeSystem.Boolean);
            type.Fields.Add(registered);
            var done = Instruction.Create(OpCodes.Ret);
            il.Emit(OpCodes.Ldsfld, registered);
            il.Emit(OpCodes.Brtrue, done);
            foreach (var attribute in providers)
            {
                var provider = ((TypeReference)attribute.ConstructorArguments[0].Value).Resolve();
                var contract = attribute.AttributeType.Name switch
                {
                    "BindingTargetProviderAttribute" => "IBindingTargetProvider",
                    "ConverterProviderAttribute" => "IConverterProvider",
                    _ => "IGlobalViewModelProvider"
                };
                if (!Implements(provider, Prefix + contract))
                    throw new InvalidOperationException(provider.FullName + " must implement " + contract);
                var ctor = provider.Methods.SingleOrDefault(m => m.IsConstructor && !m.IsStatic && m.IsPublic && !m.HasParameters);
                if (ctor == null || provider.IsAbstract || provider.HasGenericParameters)
                    throw new InvalidOperationException("Provider requires a concrete type with a public parameterless constructor: " + provider.FullName);
                if (attribute.AttributeType.Name == "GlobalViewModelProviderAttribute")
                {
                    var factory = new MethodDefinition("CreateGlobal" + type.Methods.Count,
                        MethodAttributes.Private | MethodAttributes.Static, RuntimeType("IGlobalViewModelProvider"));
                    type.Methods.Add(factory);
                    factory.Body.GetILProcessor().Emit(OpCodes.Newobj, _module.ImportReference(ctor));
                    factory.Body.GetILProcessor().Emit(OpCodes.Ret);
                    var add = Runtime("MvvmModule", "RegisterGlobalFactory");
                    il.Emit(OpCodes.Ldtoken, _module.ImportReference(provider));
                    var systemType = add.Parameters[0].ParameterType;
                    var fromHandle = new MethodReference("GetTypeFromHandle", systemType, systemType);
                    fromHandle.Parameters.Add(new ParameterDefinition(new TypeReference("System", "RuntimeTypeHandle", _module, _module.TypeSystem.CoreLibrary, true)));
                    il.Emit(OpCodes.Call, fromHandle);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, factory);
                    var delegateCtor = new MethodReference(".ctor", _module.TypeSystem.Void, add.Parameters[1].ParameterType) { HasThis = true };
                    delegateCtor.Parameters.Add(new ParameterDefinition(_module.TypeSystem.Object));
                    delegateCtor.Parameters.Add(new ParameterDefinition(_module.TypeSystem.IntPtr));
                    il.Emit(OpCodes.Newobj, delegateCtor);
                    il.Emit(OpCodes.Call, add);
                }
                else
                {
                    il.Emit(OpCodes.Newobj, _module.ImportReference(ctor));
                    il.Emit(OpCodes.Call, Runtime(attribute.AttributeType.Name == "ConverterProviderAttribute" ? "Converters" : "BindingTargetRegistry", "Register"));
                }
            }
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Stsfld, registered);
            il.Append(done);
            startup.Body.GetILProcessor().InsertBefore(startup.Body.Instructions[0], Instruction.Create(OpCodes.Call, register));
            Changed = true;
        }
        // Refresh external calls even for a previously woven incremental build.
        var previous = register.Body.Instructions.Where(i => i.Operand is MethodReference m &&
            m.DeclaringType.FullName == RegistrationType && m.Name == "Register").ToArray();
        if (previous.Select(i => ((MethodReference)i.Operand).DeclaringType.Scope.Name)
            .SequenceEqual(calls.Select(m => m.DeclaringType.Scope.Name))) return;
        var writer = register.Body.GetILProcessor();
        foreach (var old in previous) writer.Remove(old);
        var first = register.Body.Instructions[2];
        foreach (var call in calls) writer.InsertBefore(first, Instruction.Create(OpCodes.Call, _module.ImportReference(call)));
        Changed = true;
    }

    private static bool Implements(TypeDefinition type, string contract)
    {
        if (type.FullName == contract) return true;
        if (type.Interfaces.Any(i => Implements(i.InterfaceType.Resolve(), contract))) return true;
        return type.BaseType != null && type.BaseType.FullName != "System.Object" && Implements(type.BaseType.Resolve(), contract);
    }
}
