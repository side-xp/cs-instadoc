using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <see cref="DocumentationRenderer"/>: one page per type, an optional index, resolved cross-reference links,
/// and per-member anchors — all asserted on the rendered strings, without touching disk.
/// </summary>
public class DocumentationRendererTests
{
    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sample");

    private static IReadOnlyList<DocumentedType> SampleSurface()
    {
        var files = new SourceFileDiscovery().Discover([SampleRoot], []);
        var trees = new SourceParser().Parse(files);
        var compilation = new CompilationBuilder().Build(trees);
        return new ApiSurfaceExtractor().Extract(compilation, ["public", "protected"]);
    }

    private static string Page(IReadOnlyList<RenderedPage> pages, string relativePath)
        => pages.Single(page => page.RelativePath == relativePath).Content;

    [Fact(DisplayName = "Renders one page per type plus the index when requested")]
    public void Renders_one_page_per_type_plus_index()
    {
        var pages = new DocumentationRenderer().Render(SampleSurface(), includeIndex: true);

        // 5 sample types (Animal, IShape, Circle, AnimalTests, Models) + index.
        Assert.Equal(6, pages.Count);
        Assert.Contains(pages, page => page.RelativePath == "index.md");
    }

    [Fact(DisplayName = "Omits the index page when not requested")]
    public void Omits_index_when_not_requested()
    {
        var pages = new DocumentationRenderer().Render(SampleSurface(), includeIndex: false);

        Assert.DoesNotContain(pages, page => page.RelativePath == "index.md");
        Assert.Equal(5, pages.Count);
    }

    [Fact(DisplayName = "Names each page by the type's full name")]
    public void Names_pages_by_full_type_name()
    {
        var pages = new DocumentationRenderer().Render(SampleSurface(), includeIndex: false);

        Assert.Contains(pages, page => page.RelativePath == "Sample.Shapes.Circle.md");
    }

    [Fact(DisplayName = "A type page has its header, signature and summary")]
    public void Type_page_has_header_signature_summary()
    {
        var pages = new DocumentationRenderer().Render(SampleSurface(), includeIndex: false);

        var circle = Page(pages, "Sample.Shapes.Circle.md");
        Assert.Contains("# Circle", circle);
        Assert.Contains("public sealed class Circle", circle);
        Assert.Contains("A circle", circle);
    }

    [Fact(DisplayName = "Resolves a cref to another documented type as a link")]
    public void Resolves_cref_to_link()
    {
        var pages = new DocumentationRenderer().Render(SampleSurface(), includeIndex: false);

        // Circle's summary references <see cref="IShape"/>, which is also documented.
        Assert.Contains("[IShape](Sample.IShape.md)", Page(pages, "Sample.Shapes.Circle.md"));
    }

    [Fact(DisplayName = "Lists members with a heading and an explicit anchor")]
    public void Member_has_heading_and_anchor()
    {
        var pages = new DocumentationRenderer().Render(SampleSurface(), includeIndex: false);

        var circle = Page(pages, "Sample.Shapes.Circle.md");
        Assert.Contains("### Area()", circle);
        Assert.Contains("<a id=\"area\"></a>", circle);
    }

    [Fact(DisplayName = "The index groups types by namespace and links them")]
    public void Index_groups_and_links_types()
    {
        var pages = new DocumentationRenderer().Render(SampleSurface(), includeIndex: true);

        var index = Page(pages, "index.md");
        Assert.Contains("# API Reference", index);
        Assert.Contains("## Sample.Shapes", index);
        Assert.Contains("[Circle](Sample.Shapes.Circle.md)", index);
    }
}
