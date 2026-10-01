using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UIFramework.Generators;
using Xunit;

namespace UIFramework.Tests;

public class GeneratorTests
{
    [Fact]
    public void GeneratorRegistersWithoutRuntimeReflectionAndProducesTypedCalls()
    {
        var code = """
            using UIFramework;
            namespace Game;
            [UIResource("res://example.tscn")]
            public class ExampleView : UINode<string> { }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(UIContext).Assembly.Location));
        var compilation = CSharpCompilation.Create("GeneratedGame", new[] { CSharpSyntaxTree.ParseText(code) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new UIResourceGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var generated = driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();
        Assert.Contains("Register<global::Game.ExampleView>", generated);
        Assert.Contains("PushExampleViewAsync", generated);
        Assert.Contains("string args", generated);
        Assert.DoesNotContain("System.Reflection", generated);
        Assert.DoesNotContain("TabContainerNode", generated);
    }
}
