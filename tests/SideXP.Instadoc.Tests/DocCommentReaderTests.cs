using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <see cref="DocCommentReader"/>: pulling a symbol's documentation as parsed XML, with cref's already
/// resolved to IDs and <c>inheritdoc</c> left verbatim.
/// </summary>
public class DocCommentReaderTests
{
    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sample");

    private static string MixedFile => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Visibility", "Mixed.cs");

    private static Compilation Compile(params string[] files)
    {
        var trees = new SourceParser().Parse(files);
        return new CompilationBuilder().Build(trees);
    }

    [Fact(DisplayName = "Reads a documented type as a <member> element with its summary")]
    public void Reads_documented_type()
    {
        var animal = Compile(Path.Combine(SampleRoot, "Animal.cs")).GetTypeByMetadataName("Sample.Animal")!;

        var doc = new DocCommentReader().Read(animal);

        Assert.NotNull(doc);
        Assert.Equal("member", doc!.Name.LocalName);
        Assert.Equal("T:Sample.Animal", doc.Attribute("name")?.Value);
        Assert.Contains("living creature", doc.Element("summary")?.Value);
    }

    [Fact(DisplayName = "Returns null for an undocumented symbol")]
    public void Returns_null_when_undocumented()
    {
        var visible = Compile(MixedFile).GetTypeByMetadataName("Sample.Visibility.Visible")!;
        var publicMethod = visible.GetMembers("PublicMethod").Single();

        Assert.Null(new DocCommentReader().Read(publicMethod));
    }

    [Fact(DisplayName = "Resolves cref attributes to documentation IDs")]
    public void Resolves_cref_to_id()
    {
        var circle = Compile(
            Path.Combine(SampleRoot, "IShape.cs"),
            Path.Combine(SampleRoot, "Shapes", "Circle.cs"))
            .GetTypeByMetadataName("Sample.Shapes.Circle")!;

        var doc = new DocCommentReader().Read(circle)!;

        var see = doc.Descendants("see").Single();
        Assert.Equal("T:Sample.IShape", see.Attribute("cref")?.Value);
    }

    [Fact(DisplayName = "Leaves inheritdoc verbatim (not expanded by Roslyn)")]
    public void Leaves_inheritdoc_verbatim()
    {
        var circle = Compile(
            Path.Combine(SampleRoot, "IShape.cs"),
            Path.Combine(SampleRoot, "Shapes", "Circle.cs"))
            .GetTypeByMetadataName("Sample.Shapes.Circle")!;
        var area = circle.GetMembers("Area").Single();

        var doc = new DocCommentReader().Read(area)!;

        Assert.NotNull(doc.Element("inheritdoc"));
    }
}
