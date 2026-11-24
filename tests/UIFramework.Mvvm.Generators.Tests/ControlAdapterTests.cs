using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UIFramework.Mvvm.Generators;
using Xunit;

namespace UIFramework.Mvvm.Generators.Tests;

public class ControlAdapterTests
{
    private const string Native = """
        using System;
        using UIFramework.Mvvm;
        namespace Engine {
            public class Widget { public bool visible { get; set; } }
            public class Slider : Widget {
                public int value { get; set; }
                public int Step { get; set; }
                public Signal Changed { get; set; } = new Signal();
                public void Edit(int v) { value = v; Changed.Send(v); }
                public void SilentSet(int v) => value = Math.Min(v, 10);
            }
            public class Signal {
                public event Action<int> Listeners;
                public void AddListener(Action<int> action) => Listeners += action;
                public void RemoveListener(Action<int> action) => Listeners -= action;
                public void Send(int value) => Listeners?.Invoke(value);
            }
            [ControlAdapter(typeof(Widget)), BindProperty(nameof(Widget.visible))]
            internal static class WidgetAdapter { }
            [ControlAdapter(typeof(Slider))]
            [BindProperty(nameof(Slider.value), Write = nameof(Slider.SilentSet), Changed = nameof(Slider.Changed))]
            internal static class SliderAdapter { }
        }
        """;

    private const string Extra = """
        using Engine;
        using UIFramework.Mvvm;
        namespace Shop {
            [ExtendControlAdapter(typeof(Slider)), BindProperty(nameof(Slider.Step))]
            internal static class StepAdapter { }
        }
        """;

    private static readonly MetadataReference[] Framework = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Append(typeof(ControlAdapterAttribute).Assembly.Location).Distinct()
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    private static (Compilation Output, Diagnostic[] Errors) Generate(string name, string[] sources, params MetadataReference[] dependencies)
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp9);
        var compilation = CSharpCompilation.Create(name, sources.Select(s => CSharpSyntaxTree.ParseText(s, options)),
            Framework.Concat(dependencies), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(new[] { new ControlAdapterGenerator().AsSourceGenerator() }, parseOptions: options);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return (output, diagnostics.Concat(output.GetDiagnostics()).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray());
    }
    private static byte[] Emit(string name, string[] sources, params MetadataReference[] dependencies)
    {
        var result = Generate(name, sources, dependencies);
        Assert.Empty(result.Errors);
        using var stream = new MemoryStream();
        var emission = result.Output.Emit(stream);
        Assert.True(emission.Success, string.Join("\n", emission.Diagnostics));
        return stream.ToArray();
    }

    [Fact]
    public void PrimaryAndExtensionInOneAssemblyUseNativeEventsAndSilentWrites()
    {
        const string call = """
            using Engine;
            using Shop;
            using UIFramework.Mvvm;
            public static class Use {
                public static bool Run() {
                    var slider = new Slider();
                    using var target = slider.Bindable().Value;
                    int count = 0;
                    target.Changed += () => count++;
                    target.Write(20);
                    if (count != 0 || (int)target.Read() != 10) return false;
                    slider.Edit(6);
                    if (count != 1 || (int)target.Read() != 6) return false;
                    using var step = slider.Bindable().Step();
                    using var visible = slider.Bindable().Visible;
                    step.Write(3);
                    visible.Write(true);
                    var oldEvent = slider.Changed;
                    slider.Changed = new Signal();
                    target.Dispose();
                    oldEvent.Send(2);
                    return slider.Step == 3 && slider.visible && count == 1;
                }
            }
            """;
        var assembly = Assembly.Load(Emit("CombinedControls", new[] { Native, Extra, call }));
        var provider = assembly.GetCustomAttributes<BindingTargetProviderAttribute>().Single();
        BindingTargetRegistry.Register((IBindingTargetProvider)Activator.CreateInstance(provider.Type)!);
        Assert.Equal(true, assembly.GetType("Use")!.GetMethod("Run")!.Invoke(null, null));
    }

    [Fact]
    public void ExtensionsAndDerivedWrappersCompileAcrossAssemblies()
    {
        var engine = MetadataReference.CreateFromImage(Emit("EngineAdapters", new[] { Native }));
        var extra = MetadataReference.CreateFromImage(Emit("ShopAdapters", new[] { Extra }, engine));
        const string game = """
            using Engine;
            using Shop;
            using UIFramework.Mvvm;
            namespace Game {
                public class Dial : Slider { public string Caption { get; set; } }
                [ControlAdapter(typeof(Dial)), BindProperty(nameof(Dial.Caption))]
                internal static class DialAdapter { }
                public class Form {
                    public void Configure(Dial d) {
                        DialBindings b = d.Bindable();
                        b.Visible.Write(true); b.Step().Write(2); b.Value.Write(1); b.Caption.Write("dial");
                    }
                }
            }
            """;
        Emit("GameAdapters", new[] { game }, engine, extra);
        var duplicate = MetadataReference.CreateFromImage(Emit("OtherShop", new[] { Extra.Replace("namespace Shop", "namespace OtherShop") }, engine));
        var errors = Generate("Consumer", new[] { "public class Consumer {}" }, engine, extra, duplicate).Errors;
        var error = Assert.Single(errors);
        Assert.Equal("CGUI001", error.Id);
        Assert.Contains("ShopAdapters", error.GetMessage());
        Assert.Contains("OtherShop", error.GetMessage());
    }

    [Fact]
    public void OptionalControlAssemblyIsNotRequiredToExtendAnAvailableControl()
    {
        var optional = MetadataReference.CreateFromImage(Emit("OptionalTextPackage", new[] {
            "namespace Optional { public class Text { public string value { get; set; } } }" }));
        const string textAdapter = """
            using UIFramework.Mvvm;
            namespace Engine {
                [ControlAdapter(typeof(Optional.Text)), BindProperty(nameof(Optional.Text.value))]
                internal static class TextAdapter { }
            }
            """;
        var engine = MetadataReference.CreateFromImage(Emit("EngineWithOptionalText", new[] { Native, textAdapter }, optional));
        Emit("GameWithoutOptionalText", new[] { Extra }, engine);
    }

    [Fact]
    public void IntelliSenseDescribesTheRegisteredTypeAndInputCapability()
    {
        var result = Generate("Docs", new[] { Native, Extra });
        Assert.Empty(result.Errors);
        string Summary(string type, string name) => result.Output.GetTypeByMetadataName(type)!
            .GetMembers(name).Single().GetDocumentationCommentXml()!;
        Assert.Contains("Binds <c>value</c> as <c>int</c> (supports two-way binding)", Summary("Engine.SliderBindings", "Value"));
        Assert.Contains("Binds <c>Step</c> as <c>int</c> (display only)", Summary("Shop.StepAdapterBindingExtensions", "Step"));
    }

    [Fact]
    public void StaticAccessorsDelegateEventsAndCommandFactoriesReachRealControls()
    {
        const string code = """
            using System;
            using UIFramework.Mvvm;
            namespace Menu {
                public class Picker {
                    public int Current;
                    public event Action Edited;
                    public void Edit(int value) { Current = value; Edited?.Invoke(); }
                }
                [ControlAdapter(typeof(Picker))]
                [BindProperty("Quantity", Read = nameof(Read), Write = nameof(Write), Changed = nameof(Picker.Edited))]
                [BindCommand("Click", Factory = nameof(Click))]
                internal static class PickerAdapter {
                    internal static int Read(Picker t) => t.Current;
                    internal static void Write(Picker t, int value) => t.Current = value;
                    internal static ICommandTarget Click(Picker t) => new Button(t);
                }
                public class Button : ICommandTarget {
                    readonly Picker control;
                    public Button(Picker c) { control = c; }
                    public event Action Invoked { add => control.Edited += value; remove => control.Edited -= value; }
                    public void SetEnabled(bool value) {}
                    public void Dispose() {}
                }
                public static class Use {
                    public static bool Run() {
                        var picker = new Picker();
                        var target = picker.Bindable().Quantity;
                        using var button = picker.Bindable().Click;
                        int edits = 0, clicks = 0;
                        target.Write(3); target.Changed += () => edits++;
                        button.Invoked += () => clicks++;
                        picker.Edit(4);
                        if ((int)target.Read() != 4) return false;
                        target.Dispose(); picker.Edit(5);
                        return edits == 1 && clicks == 2;
                    }
                }
            }
            """;
        var assembly = Assembly.Load(Emit("PickerBindings", new[] { code }));
        var provider = assembly.GetCustomAttributes<BindingTargetProviderAttribute>().Single();
        BindingTargetRegistry.Register((IBindingTargetProvider)Activator.CreateInstance(provider.Type)!);
        Assert.Equal(true, assembly.GetType("Menu.Use")!.GetMethod("Run")!.Invoke(null, null));
    }

    [Theory]
    [InlineData("[ControlAdapter(typeof(Engine.Slider))]", "Multiple main adapters")]
    [InlineData("[ExtendControlAdapter(typeof(Engine.Slider)), BindProperty(\"value\")]", "Duplicate control member")]
    [InlineData("[ExtendControlAdapter(typeof(Engine.Slider)), BindProperty(\"Step\", Changed=\"Wrong\")]", "Wrong")]
    [InlineData("[ExtendControlAdapter(typeof(Engine.Slider)), BindCommand(\"Click\", Factory=\"Wrong\")]", "Factory")]
    [InlineData("[ExtendControlAdapter(typeof(Engine.Signal))]", "No main ControlAdapter")]
    public void InvalidDeclarationsProduceSourceErrors(string attributes, string message)
    {
        var errors = Generate("Broken", new[] { Native, "using UIFramework.Mvvm; " + attributes + " internal static class BrokenAdapter {}" }).Errors;
        Assert.Contains(errors, e => e.Id == "CGUI001" && e.GetMessage().Contains(message) && e.Location.IsInSource);
    }
}
