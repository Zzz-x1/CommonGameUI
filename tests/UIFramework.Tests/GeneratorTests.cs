using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UIFramework.Generators;
using Xunit;

namespace UIFramework.Tests;

public class GeneratorTests
{
    [Fact]
    public async Task GeneratorRegistersWithoutRuntimeReflectionAndProducesTypedCalls()
    {
        var code = """
            using UIFramework;
            using UIFramework.Generated;
            namespace Game;
            [UIResource("res://example.tscn", WindowMode = WindowInstanceMode.Multiple)]
            public class ExampleView : UINode<string> { }
            [UIResource("res://dashboard.tscn")]
            public class DashboardView : UINode { }
            [UIResource("res://confirm.tscn")]
            public class ConfirmView : DialogNode<bool> { }
            public static class Calls
            {
                public static async System.Threading.Tasks.Task Run(PanelContainerNode windows)
                {
                    await windows.OpenExampleViewAsync("creation");
                    await windows.OpenExampleViewAsync();
                    await windows.OpenDashboardViewAsync();
                }
                public static async System.Threading.Tasks.Task Verify(UIContext ui,
                    PanelContainerNode windows, StackContainerNode pages, ModalContainerNode dialogs,
                    System.Threading.CancellationToken token)
                {
                    ui.RegisterGeneratedUI();
                    await windows.OpenDashboardViewAsync(playAnimation: false, cancellationToken: token);
                    var item = await windows.OpenExampleViewAsync("typed creation", false, token);
                    if (item.Parameters != "typed creation") throw new System.Exception("Creation argument lost");
                    var page = await pages.PushExampleViewAsync("typed page", false, token);
                    if (page.Parameters != "typed page") throw new System.Exception("Stack argument lost");
                    var popup = await dialogs.OpenExampleViewAsync("typed modal", false, token);
                    if (popup.Parameters != "typed modal") throw new System.Exception("Modal argument lost");
                    await dialogs.CloseAsync(popup, false, token);
                    var result = dialogs.ShowConfirmViewAsync(false, token);
                    ((ConfirmView)dialogs.Current).Complete(true);
                    if (!await result) throw new System.Exception("Dialog result lost");
                }
                public static System.Threading.Tasks.Task Cancel(PanelContainerNode windows,
                    System.Threading.CancellationToken token) => windows.OpenExampleViewAsync("cancel", false, token);
            }
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
        Assert.Contains("WindowInstanceMode)1", generated);
        Assert.Contains("WindowInstanceMode)0", generated);
        Assert.Contains("OpenAsync<global::Game.DashboardView>(null, playAnimation, cancellationToken)", generated);
        Assert.Contains("OpenAsync<global::Game.ExampleView>(null, playAnimation, cancellationToken)", generated);
        Assert.DoesNotContain("System.Reflection", generated);
        Assert.DoesNotContain("TabContainerNode", generated);
        using var stream = new MemoryStream();
        Assert.True(output.Emit(stream).Success);
        var calls = System.Reflection.Assembly.Load(stream.ToArray()).GetType("Game.Calls")!;
        var host = new TestHost(); await using var ui = new UIContext(host, host, host);
        ui.DefaultTransition = Transitions.Select(_ => throw new Exception("Animation flag was not forwarded"));
        var windows = new PanelContainerNode(); await windows.AttachAsync(ui.Root, "Windows", new MemoryElement());
        var pages = new StackContainerNode(); await pages.AttachAsync(ui.Root, "Pages", new MemoryElement());
        var dialogs = new ModalContainerNode(); await dialogs.AttachAsync(ui.Root, "Dialogs", new MemoryElement());
        await (Task)calls.GetMethod("Verify")!.Invoke(null, new object[] { ui, windows, pages, dialogs, CancellationToken.None })!;
        Assert.Empty(host.Errors);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var task = (Task)calls.GetMethod("Cancel")!.Invoke(null, new object[] { windows, canceled.Token })!;
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(canceled.Token, failure.CancellationToken);
    }
}
