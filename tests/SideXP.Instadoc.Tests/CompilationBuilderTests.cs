using Microsoft.CodeAnalysis;
using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <see cref="CompilationBuilder"/>: the tolerant compilation must resolve types declared in the sources
/// and in the BCL, while degrading unresolved external types to error symbols instead of failing.
/// </summary>
public class CompilationBuilderTests
{

    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sample");

    private static string ExternalFile => Path.Combine(AppContext.BaseDirectory, "Fixtures", "External", "Widget.cs");

    private static string ImplicitFile => Path.Combine(AppContext.BaseDirectory, "Fixtures", "ImplicitUsings", "UsesImplicit.cs");

    private static CSharpCompilationFor BuildFrom(params string[] files)
    {
        var trees = new SourceParser().Parse(files);
        var compilation = new CompilationBuilder().Build(trees);
        return new CSharpCompilationFor(compilation);
    }

    [Fact(DisplayName = "Resolves a type declared in the sources")]
    public void Resolves_a_source_declared_type()
    {
        var compilation = BuildFrom(Path.Combine(SampleRoot, "Animal.cs"));

        var animal = compilation.GetType("Sample.Animal");

        Assert.NotNull(animal);
        Assert.Equal(TypeKind.Class, animal!.TypeKind);
    }

    [Fact(DisplayName = "Resolves BCL types from the reference assemblies")]
    public void Resolves_bcl_types()
    {
        var compilation = BuildFrom(Path.Combine(SampleRoot, "Animal.cs"));

        var nameType = compilation.GetType("Sample.Animal")!
            .GetMembers("Name")
            .OfType<IPropertySymbol>()
            .Single()
            .Type;

        // The Name property is a `string`; it only maps to the special System.String type when the BCL is referenced.
        Assert.Equal(SpecialType.System_String, nameType.SpecialType);
    }

    [Fact(DisplayName = "Resolves cross-references between own types")]
    public void Resolves_cross_references_between_own_types()
    {
        // Circle implements IShape; both are declared in the sources, so the interface must resolve.
        var compilation = BuildFrom(
            Path.Combine(SampleRoot, "IShape.cs"),
            Path.Combine(SampleRoot, "Shapes", "Circle.cs"));

        var circle = compilation.GetType("Sample.Shapes.Circle");

        Assert.NotNull(circle);
        Assert.Contains(circle!.AllInterfaces, i => i.Name == "IShape");
    }

    [Fact(DisplayName = "Degrades an unresolved external type to an error symbol")]
    public void Degrades_unresolved_external_type_to_error_symbol()
    {
        var compilation = BuildFrom(ExternalFile);

        var targetType = compilation.GetType("Sample.External.Widget")!
            .GetMembers("Target")
            .OfType<IPropertySymbol>()
            .Single()
            .Type;

        // UnityEngine.GameObject is not on the reference path: it must be an error symbol, not a crash.
        Assert.Equal(TypeKind.Error, targetType.TypeKind);
        Assert.IsAssignableFrom<IErrorTypeSymbol>(targetType);
    }

    [Fact(DisplayName = "Resolves short-name BCL types via the injected implicit usings")]
    public void Resolves_short_name_bcl_types_via_implicit_usings()
    {
        var compilation = BuildFrom(ImplicitFile);

        // CancellationToken is written with no using; it resolves only because the implicit usings are injected.
        var tokenType = compilation.GetType("Sample.Implicit.UsesImplicit")!
            .GetMembers("Token")
            .OfType<IPropertySymbol>()
            .Single()
            .Type;

        Assert.NotEqual(TypeKind.Error, tokenType.TypeKind);
        Assert.True(tokenType.IsValueType);
        Assert.Equal("CancellationToken", tokenType.Name);
    }

    /// <summary>
    /// Tiny wrapper to keep the type-lookup helper close to the tests.
    /// </summary>
    private sealed class CSharpCompilationFor(Compilation compilation)
    {
        public INamedTypeSymbol? GetType(string metadataName) => compilation.GetTypeByMetadataName(metadataName);
    }
    
}
