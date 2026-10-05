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
        public override bool WillProcess(ICompiledAssembly assembly) => !assembly.Name.StartsWith("UIFramework.") && assembly.References.Any(r => Path.GetFileName(r) == "UIFramework.Mvvm.dll");
        public override ILPostProcessResult Process(ICompiledAssembly assembly)
        {
            var resolver = new DefaultAssemblyResolver(); foreach (var reference in assembly.References) resolver.AddSearchDirectory(Path.GetDirectoryName(reference));
            using var pe = new MemoryStream(assembly.InMemoryAssembly.PeData); using var pdb = new MemoryStream(assembly.InMemoryAssembly.PdbData);
            using var module = ModuleDefinition.ReadModule(pe, new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = pdb.Length != 0, SymbolStream = pdb, SymbolReaderProvider = new Mono.Cecil.Cil.PortablePdbReaderProvider() });
            using var runtime = ModuleDefinition.ReadModule(assembly.References.Single(r => Path.GetFileName(r) == "UIFramework.Mvvm.dll"));
            var weaver = new ViewModelWeaver(module, runtime); weaver.Process();
            if (!weaver.Changed) return null;
            var engine = resolver.Resolve(new AssemblyNameReference("UnityEngine.CoreModule", new System.Version(0, 0, 0, 0)));
            var preserve = module.ImportReference(engine.MainModule.GetType("UnityEngine.Scripting.PreserveAttribute").Methods.Single(m => m.IsConstructor && !m.HasParameters));
            Preserve(module.Types);
            void Preserve(System.Collections.Generic.IEnumerable<TypeDefinition> types)
            {
                foreach (var type in types)
                {
                    if (type.CustomAttributes.Any(a => a.AttributeType.FullName == "UIFramework.Mvvm.WovenViewModelAttribute") || type.FullName == "UIFramework.Mvvm.Generated.ConverterProvider")
                    { type.CustomAttributes.Add(new CustomAttribute(preserve)); foreach (var method in type.Methods) method.CustomAttributes.Add(new CustomAttribute(preserve)); }
                    Preserve(type.NestedTypes);
                }
            }
            using var output = new MemoryStream(); using var symbols = new MemoryStream();
            module.Write(output, new WriterParameters { WriteSymbols = pdb.Length != 0, SymbolStream = symbols, SymbolWriterProvider = new Mono.Cecil.Cil.PortablePdbWriterProvider() });
            return new ILPostProcessResult(new InMemoryAssembly(output.ToArray(), symbols.ToArray()));
        }
    }
}
