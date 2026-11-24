using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace UIFramework.Mvvm.Generators;

[Generator]
public sealed partial class ControlAdapterGenerator : IIncrementalGenerator
{
    private const string Api = "UIFramework.Mvvm.";
    private const string Qualified = "global::" + Api;
    private static readonly DiagnosticDescriptor Invalid = new("CGUI001", "Invalid control adapter",
        "{0}", "CommonGameUI", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider.CreateSyntaxProvider(
            (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
            (syntax, token) => syntax.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntax.Node, token))
            .Where(type => type != null && type.GetAttributes().Any(IsDeclaration));
        context.RegisterSourceOutput(context.CompilationProvider.Combine(types.Collect()),
            (output, input) => Generate(output, input.Left, input.Right));
    }

    private static bool IsDeclaration(AttributeData attribute) =>
        attribute.AttributeClass?.ToDisplayString() is Api + "ControlAdapterAttribute" or Api + "ExtendControlAdapterAttribute";
    private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static bool Same(ISymbol? a, ISymbol? b) => SymbolEqualityComparer.Default.Equals(a, b);
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
    private static string MemberName(string key) => key.Length == 0 ? key : char.ToUpperInvariant(key[0]) + key.Substring(1);
    private static string Namespace(INamedTypeSymbol type) =>
        type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
    private static string InNamespace(string ns, string member) => "global::" + (ns.Length == 0 ? "" : ns + ".") + member;
    private static string? Option(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value as string;

    private sealed class Adapter
    {
        internal readonly INamedTypeSymbol Declaration, Control;
        internal readonly bool Extension;
        internal readonly string Namespace;
        internal readonly List<Member> Members = new();
        internal string Wrapper => InNamespace(Namespace, Control.Name + "Bindings");
        internal Adapter(INamedTypeSymbol declaration, INamedTypeSymbol control, bool extension)
        { Declaration = declaration; Control = control; Extension = extension; Namespace = ControlAdapterGenerator.Namespace(declaration); }
    }

    private sealed class Member
    {
        internal readonly string Key, Expression, Summary;
        internal readonly bool Command;
        internal string PublicName => MemberName(Key);
        internal Member(string key, string expression, ITypeSymbol? valueType = null, bool observable = false)
        {
            Key = key; Expression = expression; Command = valueType == null;
            string native = "<c>" + SecurityElement.Escape(key) + "</c>";
            string type = SecurityElement.Escape(valueType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)) ?? "";
            Summary = Command ? $"Command target for {native}." :
                $"Binds {native} as <c>{type}</c> ({(observable ? "supports two-way binding" : "display only")}).";
        }
    }

    private static void Generate(SourceProductionContext output, Compilation compilation, ImmutableArray<INamedTypeSymbol?> symbols)
    {
        if (compilation.GetTypeByMetadataName(Api + "ControlAdapterAttribute") == null) return;
        var wrappers = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        var keys = new Dictionary<INamedTypeSymbol, Dictionary<string, string>>(SymbolEqualityComparer.Default);
        var adapters = new List<Adapter>();
        bool failed = false;
        void Error(string message, Location? location)
        { failed = true; output.ReportDiagnostic(Diagnostic.Create(Invalid, location, message)); }
        void AddWrapper(INamedTypeSymbol control, string wrapper, Location? location)
        {
            if (wrappers.TryGetValue(control, out var first))
                Error($"Multiple main adapters for {control}: {first} and {wrapper}. Use ExtendControlAdapter to add members.", location);
            else wrappers.Add(control, wrapper);
        }
        void AddKey(INamedTypeSymbol control, string key, string origin, Location? location)
        {
            if (!keys.TryGetValue(control, out var members)) keys.Add(control, members = new Dictionary<string, string>());
            string publicName = MemberName(key);
            if (members.TryGetValue(publicName, out var previous))
                Error($"Duplicate control member {control}.{publicName}: {previous} and {origin}.", location);
            else members.Add(publicName, origin);
        }
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                string? name = attribute.AttributeClass?.Name;
                if (name is not ("ControlAdapterMapAttribute" or "ControlAdapterMemberAttribute") ||
                    attribute.AttributeClass!.ContainingNamespace.ToDisplayString() != "UIFramework.Mvvm") continue;
                var args = attribute.ConstructorArguments;
                // The consumer may reference the adapter assembly without importing an optional control assembly.
                if (args.Length != 2 || !(args[0].Value is INamedTypeSymbol control) || control.TypeKind == TypeKind.Error) continue;
                if (name == "ControlAdapterMapAttribute" && args[1].Value is INamedTypeSymbol wrapper && wrapper.TypeKind != TypeKind.Error)
                    AddWrapper(control, Name(wrapper), Location.None);
                if (name == "ControlAdapterMemberAttribute" && args[1].Value is string key)
                    AddKey(control, key, assembly.Name, Location.None);
            }
        }
        foreach (var declaration in symbols.OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                     .OrderBy(symbol => Name(symbol), StringComparer.Ordinal))
        {
            var attributes = declaration.GetAttributes();
            var declarations = attributes.Where(IsDeclaration).ToArray();
            if (declarations.Length != 1 || declarations[0].ConstructorArguments.Length != 1 ||
                !(declarations[0].ConstructorArguments[0].Value is INamedTypeSymbol control) ||
                control.TypeKind != TypeKind.Class || control.IsGenericType)
            { Error($"{declaration} needs one main or extension declaration for a non-generic control class.", declaration.Locations[0]); continue; }
            if (control.IsStatic || !compilation.IsSymbolAccessibleWithin(control, compilation.Assembly))
            { Error($"The generated adapter cannot access instances of {control}.", declaration.Locations[0]); continue; }
            var adapter = new Adapter(declaration, control, declarations[0].AttributeClass!.Name == "ExtendControlAdapterAttribute");
            adapters.Add(adapter);
            if (!adapter.Extension) AddWrapper(control, adapter.Wrapper, declaration.Locations[0]);
            foreach (var attribute in attributes)
            {
                var type = attribute.AttributeClass?.ToDisplayString();
                if (type is not (Api + "BindPropertyAttribute" or Api + "BindCommandAttribute")) continue;
                var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? declaration.Locations[0];
                try
                {
                    string key = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "";
                    if (!SyntaxFacts.IsValidIdentifier(MemberName(key)))
                        throw new InvalidOperationException($"'{key}' does not produce a valid C# binding member name.");
                    AddKey(control, key, declaration.ToDisplayString(), location);
                    var member = ResolveMember(compilation, adapter, attribute, key, type == Api + "BindCommandAttribute");
                    adapter.Members.Add(member);
                }
                catch (InvalidOperationException ex) { Error(ex.Message, location); }
            }
        }
        foreach (var adapter in adapters.Where(adapter => adapter.Extension && !wrappers.ContainsKey(adapter.Control)))
            Error($"No main ControlAdapter was found for {adapter.Control} extended by {adapter.Declaration}.", adapter.Declaration.Locations[0]);
        if (failed || adapters.Count == 0) return;

        var source = new StringBuilder("// <auto-generated/>\n#nullable disable\n");
        foreach (var adapter in adapters)
        {
            if (!adapter.Extension)
                source.AppendLine($"[assembly: {Qualified}ControlAdapterMapAttribute(typeof({Name(adapter.Control)}), typeof({adapter.Wrapper}))]");
            foreach (var member in adapter.Members)
                source.AppendLine($"[assembly: {Qualified}ControlAdapterMemberAttribute(typeof({Name(adapter.Control)}), {Literal(member.Key)})]");
        }
        const string provider = "UIFramework.Mvvm.Generated.ControlTargetProvider";
        source.AppendLine($"[assembly: {Qualified}BindingTargetProviderAttribute(typeof(global::{provider}))]");
        source.AppendLine("namespace UIFramework.Mvvm.Generated {");
        source.AppendLine($"internal sealed class ControlTargetProvider : {Qualified}IBindingTargetProvider {{");
        source.AppendLine("public ControlTargetProvider() {}");
        source.AppendLine($"public global::System.Collections.Generic.IEnumerable<{Qualified}BindingTargetDefinition> GetTargets() {{");
        foreach (var member in adapters.SelectMany(adapter => adapter.Members).Where(member => !member.Command))
            source.AppendLine("yield return " + member.Expression + ";");
        source.AppendLine("yield break;\n} } }");
        foreach (var adapter in adapters) WriteSurface(source, adapter, wrappers, keys);
        output.AddSource("ControlBindings.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void WriteSurface(StringBuilder code, Adapter adapter, Dictionary<INamedTypeSymbol, string> wrappers,
        Dictionary<INamedTypeSymbol, Dictionary<string, string>> keys)
    {
        string control = Name(adapter.Control);
        string wrapper = wrappers[adapter.Control];
        string wrappedType = adapter.Control.Name + "Bindings";
        string visibility = "public";
        for (var type = adapter.Control; type != null; type = type.ContainingType)
            if (type.DeclaredAccessibility != Accessibility.Public) visibility = "internal";
        var extensionClass = adapter.Extension ? adapter.Declaration.Name + "BindingExtensions" : wrappedType + "Extensions";
        if (adapter.Namespace != "") code.AppendLine($"namespace {adapter.Namespace} {{");
        if (!adapter.Extension)
        {
            var parent = adapter.Control.BaseType;
            while (parent != null && !wrappers.ContainsKey(parent)) parent = parent.BaseType;
            var baseWrapper = parent == null ? Qualified + "BindableMembers" : wrappers[parent];
            code.AppendLine($"{visibility} class {wrappedType} : {baseWrapper} {{");
            code.AppendLine($"public {wrappedType}({control} control) : base(control) {{}}");
            foreach (var member in adapter.Members)
            {
                string hiding = "";
                for (var type = parent; type != null; type = type.BaseType)
                    if (keys.TryGetValue(type, out var names) && names.ContainsKey(member.PublicName)) { hiding = "new "; break; }
                string result = member.Command ? Qualified + "ICommandTarget" : Qualified + "IBindingTarget";
                string expression = member.Command ? member.Expression + $"(({control})base.Control)" : $"base.Property({Literal(member.Key)})";
                code.AppendLine($"/// <summary>{member.Summary}</summary>");
                code.AppendLine($"public {hiding}{result} {member.PublicName} => {expression};");
            }
            code.AppendLine("}");
        }
        code.AppendLine($"{visibility} static class {extensionClass} {{");
        if (!adapter.Extension)
            code.AppendLine($"public static {wrapper} Bindable(this {control} control) => new {wrapper}(control);");
        else foreach (var member in adapter.Members)
        {
            string result = member.Command ? Qualified + "ICommandTarget" : Qualified + "IBindingTarget";
            string expression = member.Command ? member.Expression + $"(({control})bindings.Control)" : $"bindings.Property({Literal(member.Key)})";
            code.AppendLine($"/// <summary>{member.Summary}</summary>");
            code.AppendLine($"public static {result} {member.PublicName}(this {wrapper} bindings) => {expression};");
        }
        code.AppendLine("}");
        if (adapter.Namespace != "") code.AppendLine("}");
    }
}
