using System.IO;
using System.Linq;
using Mono.Cecil;
using UIFramework.Mvvm.Weaver;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace UIFramework.Mvvm.Unity.CodeGen
{
    public sealed class MvvmILPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => new MvvmILPostProcessor();
        public override bool WillProcess(ICompiledAssembly assembly) => assembly.Name != "Unity.UIFramework.Mvvm.CodeGen" && assembly.References.Any(r => Path.GetFileName(r) == "UIFramework.Mvvm.dll");
        public override ILPostProcessResult Process(ICompiledAssembly assembly)
        {
            var resolver = new DefaultAssemblyResolver(); foreach (var reference in assembly.References) resolver.AddSearchDirectory(Path.GetDirectoryName(reference));
            using var pe = new MemoryStream(assembly.InMemoryAssembly.PeData); using var pdb = new MemoryStream(assembly.InMemoryAssembly.PdbData);
            using var module = ModuleDefinition.ReadModule(pe, new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = pdb.Length != 0, SymbolStream = pdb, SymbolReaderProvider = new Mono.Cecil.Cil.PortablePdbReaderProvider() });
            if (!module.AssemblyReferences.Any(r => r.Name == "UIFramework.Mvvm")) return null;
            using var runtime = ModuleDefinition.ReadModule(assembly.References.Single(r => Path.GetFileName(r) == "UIFramework.Mvvm.dll"));
            var weaver = new ViewModelWeaver(module, runtime); weaver.Process();
            var providers = module.Assembly.CustomAttributes.Where(a => a.AttributeType.FullName == "UIFramework.Mvvm.BindingTargetProviderAttribute")
                .Select(a => ((TypeReference)a.ConstructorArguments[0].Value).FullName).ToArray();
            if (!weaver.Changed && providers.Length == 0) return null;
            var engine = resolver.Resolve(new AssemblyNameReference("UnityEngine.CoreModule", new System.Version(0, 0, 0, 0)));
            var preserve = module.ImportReference(engine.MainModule.GetType("UnityEngine.Scripting.PreserveAttribute").Methods.Single(m => m.IsConstructor && !m.HasParameters));
            if (weaver.Registration != null)
            {
                var alwaysLink = engine.MainModule.GetType("UnityEngine.Scripting.AlwaysLinkAssemblyAttribute");
                if (!module.Assembly.CustomAttributes.Any(a => a.AttributeType.FullName == alwaysLink.FullName))
                    module.Assembly.CustomAttributes.Add(new CustomAttribute(module.ImportReference(alwaysLink.Methods.Single(m => m.IsConstructor))));
                var attribute = engine.MainModule.GetType("UnityEngine.RuntimeInitializeOnLoadMethodAttribute");
                var ctor = attribute.Methods.Single(m => m.IsConstructor && m.Parameters.Count == 1);
                var hook = new CustomAttribute(module.ImportReference(ctor));
                hook.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference(ctor.Parameters[0].ParameterType),
                    ctor.Parameters[0].ParameterType.Resolve().Fields.Single(f => f.Name == "SubsystemRegistration").Constant));
                if (!weaver.Registration.CustomAttributes.Any(a => a.AttributeType.FullName == attribute.FullName))
                    weaver.Registration.CustomAttributes.Add(hook);
                if (assembly.Defines.Contains("UNITY_EDITOR"))
                {
                    var editor = resolver.Resolve(new AssemblyNameReference("UnityEditor.CoreModule", new System.Version(0, 0, 0, 0)));
                    var editorHook = editor.MainModule.GetType("UnityEditor.InitializeOnLoadMethodAttribute");
                    if (!weaver.Registration.CustomAttributes.Any(a => a.AttributeType.FullName == editorHook.FullName))
                        weaver.Registration.CustomAttributes.Add(new CustomAttribute(module.ImportReference(editorHook.Methods.Single(m => m.IsConstructor))));
                }
            }
            Preserve(module.Types);
            void Preserve(System.Collections.Generic.IEnumerable<TypeDefinition> types)
            {
                foreach (var type in types)
                {
                    if (type.CustomAttributes.Any(a => a.AttributeType.FullName == "UIFramework.Mvvm.WovenViewModelAttribute") || type.FullName == "UIFramework.Mvvm.Generated.ConverterProvider" || providers.Contains(type.FullName))
                    { if (!type.CustomAttributes.Any(a => a.AttributeType.FullName == preserve.DeclaringType.FullName)) type.CustomAttributes.Add(new CustomAttribute(preserve)); foreach (var method in type.Methods) if (!method.CustomAttributes.Any(a => a.AttributeType.FullName == preserve.DeclaringType.FullName)) method.CustomAttributes.Add(new CustomAttribute(preserve)); }
                    Preserve(type.NestedTypes);
                }
            }
            using var output = new MemoryStream(); using var symbols = new MemoryStream();
            module.Write(output, new WriterParameters { WriteSymbols = pdb.Length != 0, SymbolStream = symbols, SymbolWriterProvider = new Mono.Cecil.Cil.PortablePdbWriterProvider() });
            return new ILPostProcessResult(new InMemoryAssembly(output.ToArray(), symbols.ToArray()));
        }
    }
}
