using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UIFramework.Mvvm;
using UIFramework.Mvvm.Weaver;
using Xunit;

namespace UIFramework.Mvvm.Tests;

public class WeaverTests
{
    private static ModuleDefinition Compile(string body)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(ViewModelAttribute).Assembly.Location).Distinct().Select(path => MetadataReference.CreateFromFile(path));
        var tree = CSharpSyntaxTree.ParseText("using System; using System.Threading.Tasks; using UIFramework.Mvvm; " + body, path: "Vm.cs", encoding: Encoding.UTF8);
        var compilation = CSharpCompilation.Create("VmFixture", new[] { tree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));
        var pe = new MemoryStream(); var pdb = new MemoryStream(); var result = compilation.Emit(pe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        Assert.True(result.Success, string.Join("\n", result.Diagnostics)); pe.Position = pdb.Position = 0;
        var resolver = new DefaultAssemblyResolver(); resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)); resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(ViewModelAttribute).Assembly.Location));
        return ModuleDefinition.ReadModule(pe, new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = true, SymbolStream = pdb, SymbolReaderProvider = new PortablePdbReaderProvider() });
    }
    private static ViewModelWeaver Weaver(ModuleDefinition module, out ModuleDefinition runtime)
    { runtime = ModuleDefinition.ReadModule(typeof(ViewModelAttribute).Assembly.Location); return new(module, runtime); }
    [Theory]
    [InlineData("[ViewModel] public class Vm { [Command] public async void Execute() { await Task.Yield(); } }")]
    [InlineData("[ViewModel] public class Vm { [FieldNotify(nameof(B))] public int A {get;set;} [FieldNotify(nameof(A))] public int B {get;set;} }")]
    [InlineData("[ViewModel] public class Vm { [FieldNotify(\"Missing\")] public int A {get;set;} }")]
    [InlineData("[ViewModel] public class Vm { [Command] public void Buy() {} [Command] public void Buy(int id) {} }")]
    [InlineData("[ViewModel] public class Vm { [Command] public void Buy(ref int id) {} }")]
    public void UnsupportedContractsFailTheBuild(string source)
    { using var module = Compile(source); var weaver = Weaver(module, out var runtime); using (runtime) Assert.Throws<InvalidOperationException>(() => weaver.Process()); }
    [Fact] public void RepeatedWeavingPreservesPortableSymbolsAndAbstractBase()
    {
        using var module = Compile("[ViewModel] public abstract class Base { [FieldNotify] public int Gold { get; set; } } [ViewModel] public class Vm : Base { [FieldNotify(nameof(Gold))] public bool Rich => Gold > 10; }");
        var weaver = Weaver(module, out var runtime); using (runtime)
        {
            Assert.Equal(2, weaver.Process()); Assert.Equal(0, weaver.Process());
            var original = module.Types.Single(t => t.Name == "Base").Methods.Single(m => m.Name == "__mvvmOriginal_set_Gold"); Assert.NotEmpty(original.DebugInformation.SequencePoints);
            using var pe = new MemoryStream(); using var pdb = new MemoryStream(); module.Write(pe, new WriterParameters { WriteSymbols = true, SymbolStream = pdb, SymbolWriterProvider = new PortablePdbWriterProvider() }); pe.Position = pdb.Position = 0;
            using var reread = ModuleDefinition.ReadModule(pe, new ReaderParameters { ReadSymbols = true, SymbolStream = pdb, SymbolReaderProvider = new PortablePdbReaderProvider() });
            Assert.NotEmpty(reread.Types.Single(t => t.Name == "Base").Methods.Single(m => m.Name == "__mvvmOriginal_set_Gold").DebugInformation.SequencePoints);
        }
    }
}
