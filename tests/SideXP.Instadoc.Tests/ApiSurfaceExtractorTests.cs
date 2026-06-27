using Microsoft.CodeAnalysis;
using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <see cref="ApiSurfaceExtractor"/>: which types and members are selected for the requested visibility,
/// driven by the symbol model (so undocumented members still appear).
/// </summary>
public class ApiSurfaceExtractorTests
{
    private static readonly string[] PublicAndProtected = ["public", "protected"];

    private static string MixedFile => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Visibility", "Mixed.cs");

    private static IReadOnlyList<DocumentedType> ExtractFrom(string file, params string[] visibility)
    {
        var trees = new SourceParser().Parse([file]);
        var compilation = new CompilationBuilder().Build(trees);
        return new ApiSurfaceExtractor().Extract(compilation, visibility);
    }

    private static DocumentedType TypeNamed(IReadOnlyList<DocumentedType> surface, string name)
        => surface.Single(documented => documented.Symbol.Name == name);

    [Fact(DisplayName = "Selects public types and skips lower-visibility ones")]
    public void Selects_public_types_skips_internal()
    {
        var surface = ExtractFrom(MixedFile, PublicAndProtected);

        var names = surface.Select(documented => documented.Symbol.Name).ToList();
        Assert.Contains("Visible", names);
        Assert.DoesNotContain("Hidden", names); // internal
    }

    [Fact(DisplayName = "Selects members matching the requested visibility")]
    public void Selects_members_matching_visibility()
    {
        var surface = ExtractFrom(MixedFile, PublicAndProtected);

        var members = TypeNamed(surface, "Visible").Members.Select(member => member.Name).ToList();

        Assert.Contains("PublicProperty", members);
        Assert.Contains("ProtectedProperty", members);
        Assert.Contains("PublicMethod", members);

        Assert.DoesNotContain("InternalProperty", members);
        Assert.DoesNotContain("_privateField", members);
        Assert.DoesNotContain("PrivateMethod", members);
    }

    [Fact(DisplayName = "Includes undocumented members (enumeration from symbols, not XML docs)")]
    public void Includes_undocumented_members()
    {
        var surface = ExtractFrom(MixedFile, PublicAndProtected);

        // PublicMethod carries no /// comment, yet it must still be part of the surface.
        var publicMethod = TypeNamed(surface, "Visible").Members
            .OfType<IMethodSymbol>()
            .SingleOrDefault(method => method.Name == "PublicMethod");

        Assert.NotNull(publicMethod);
        Assert.Equal(string.Empty, publicMethod!.GetDocumentationCommentXml());
    }

    [Fact(DisplayName = "Excludes property accessor methods and compiler-generated members")]
    public void Excludes_accessors_and_generated_members()
    {
        var surface = ExtractFrom(MixedFile, PublicAndProtected);

        var members = TypeNamed(surface, "Visible").Members;

        // No get_/set_ accessor methods leak in as standalone members.
        Assert.DoesNotContain(members, member => member.Name.StartsWith("get_") || member.Name.StartsWith("set_"));
        // No compiler-generated backing field for the auto-properties.
        Assert.DoesNotContain(members, member => member.Name.Contains("BackingField"));
    }

    [Fact(DisplayName = "Includes explicit constructors as members")]
    public void Includes_explicit_constructors()
    {
        var surface = ExtractFrom(MixedFile, PublicAndProtected);

        var members = TypeNamed(surface, "Visible").Members;

        Assert.Contains(members, member => member is IMethodSymbol { MethodKind: MethodKind.Constructor });
    }

    [Fact(DisplayName = "Lists nested types as their own pages, not as members of the parent")]
    public void Nested_types_are_their_own_pages()
    {
        var surface = ExtractFrom(MixedFile, PublicAndProtected);

        var names = surface.Select(documented => documented.Symbol.Name).ToList();
        Assert.Contains("Outer", names);
        Assert.Contains("Inner", names);

        // Inner is not a "member" of Outer; it stands on its own.
        Assert.DoesNotContain(TypeNamed(surface, "Outer").Members, member => member.Name == "Inner");
    }
}
