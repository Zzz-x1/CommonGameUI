using Mono.Cecil;
using UIFramework.Mvvm.Weaver;

var path = Path.GetFullPath(args[0]);
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(path));
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
foreach (var reference in args.Skip(2)) resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(reference)));
resolver.AddSearchDirectory(AppContext.BaseDirectory);
bool symbols = File.Exists(Path.ChangeExtension(path, ".pdb"));
using var module = ModuleDefinition.ReadModule(path, new ReaderParameters { ReadSymbols = symbols, InMemory = true, AssemblyResolver = resolver });
using var runtime = ModuleDefinition.ReadModule(typeof(UIFramework.Mvvm.ViewModelAttribute).Assembly.Location);
var weaver = new ViewModelWeaver(module, runtime);
var dependencies = new List<MethodReference>();
foreach (var dependency in File.ReadLines(args[1]).Distinct(StringComparer.OrdinalIgnoreCase))
{
    using var stream = File.OpenRead(dependency);
    using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
    if (!pe.HasMetadata) continue;
    using var implementation = ModuleDefinition.ReadModule(dependency);
    var registration = implementation.GetType(ViewModelWeaver.RegistrationType);
    if (registration != null)
        dependencies.Add(module.ImportReference(registration.Methods.Single(m => m.Name == "Register")));
}
int woven = weaver.Process(dependencies);
if (weaver.Changed) module.Write(path, new WriterParameters { WriteSymbols = symbols });
Console.WriteLine($"MVVM woven: {woven} ({Path.GetFileName(path)})");
