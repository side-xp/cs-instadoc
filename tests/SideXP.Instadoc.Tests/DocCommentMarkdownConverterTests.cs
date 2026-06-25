using System.Xml.Linq;
using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <see cref="DocCommentMarkdownConverter"/>: each documentation tag maps to the expected Markdown, and
/// cross-references go through the injected <see cref="CrefResolver"/>.
/// </summary>
public class DocCommentMarkdownConverterTests
{

    private static string Convert(string memberXml, CrefResolver? resolver = null)
        => new DocCommentMarkdownConverter(resolver).Convert(XElement.Parse(memberXml));

    [Fact(DisplayName = "Renders summary prose with inline code")]
    public void Renders_summary_with_inline_code()
    {
        var md = Convert("<member><summary>Hello <c>world</c>.</summary></member>");

        Assert.Equal("Hello `world`.", md);
    }

    [Fact(DisplayName = "Collapses the whitespace and newlines from /// formatting")]
    public void Normalizes_whitespace()
    {
        var md = Convert("<member><summary>\n    Multi\n    line\n    text.\n  </summary></member>");

        Assert.Equal("Multi line text.", md);
    }

    [Fact(DisplayName = "Renders parameters as a labelled list")]
    public void Renders_parameters()
    {
        var md = Convert("<member><param name=\"x\">The X.</param><param name=\"y\">The Y.</param></member>");

        Assert.Contains("**Parameters**", md);
        Assert.Contains("- `x`: The X.", md);
        Assert.Contains("- `y`: The Y.", md);
    }

    [Fact(DisplayName = "Renders returns as a labelled section")]
    public void Renders_returns()
    {
        var md = Convert("<member><returns>The result.</returns></member>");

        Assert.Contains("**Returns**", md);
        Assert.Contains("The result.", md);
    }

    [Fact(DisplayName = "Renders see langword as inline code")]
    public void Renders_langword()
    {
        var md = Convert("<member><summary>Returns <see langword=\"null\"/> on miss.</summary></member>");

        Assert.Equal("Returns `null` on miss.", md);
    }

    [Fact(DisplayName = "Renders an unresolved cref as inline code by default")]
    public void Renders_cref_default()
    {
        var md = Convert("<member><summary>See <see cref=\"T:Sample.IShape\"/>.</summary></member>");

        Assert.Equal("See `IShape`.", md);
    }

    [Fact(DisplayName = "Routes cref through the injected resolver")]
    public void Routes_cref_through_resolver()
    {
        CrefResolver linkify = (id, text) => $"[{text ?? "?"}]({id}.md)";

        var md = Convert("<member><summary>See <see cref=\"T:Sample.IShape\">the shape</see>.</summary></member>", linkify);

        Assert.Equal("See [the shape](T:Sample.IShape.md).", md);
    }

    [Fact(DisplayName = "Renders a code block as a fenced GFM block")]
    public void Renders_code_block()
    {
        var md = Convert("<member><remarks><code>var x = 1;</code></remarks></member>");

        Assert.Contains("```csharp", md);
        Assert.Contains("var x = 1;", md);
        Assert.Contains("```", md);
    }

    [Fact(DisplayName = "Renders a bullet list")]
    public void Renders_bullet_list()
    {
        var md = Convert(
            "<member><remarks><list type=\"bullet\">" +
            "<item><description>One</description></item>" +
            "<item><description>Two</description></item>" +
            "</list></remarks></member>");

        Assert.Contains("- One", md);
        Assert.Contains("- Two", md);
    }

    [Fact(DisplayName = "Renders exceptions with their cref and reason")]
    public void Renders_exceptions()
    {
        var md = Convert("<member><exception cref=\"T:System.ArgumentNullException\">when null.</exception></member>");

        Assert.Contains("**Exceptions**", md);
        Assert.Contains("- `ArgumentNullException`: when null.", md);
    }

    [Fact(DisplayName = "Splits paragraphs on <para>")]
    public void Splits_paragraphs()
    {
        var md = Convert("<member><summary>First.<para>Second.</para></summary></member>");

        Assert.Equal("First.\n\nSecond.", md);
    }

    [Fact(DisplayName = "End to end: reads a real symbol's doc and converts it")]
    public void End_to_end_from_reader()
    {
        var sampleRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sample");
        var trees = new SourceParser().Parse([Path.Combine(sampleRoot, "Animal.cs")]);
        var compilation = new CompilationBuilder().Build(trees);
        var animal = compilation.GetTypeByMetadataName("Sample.Animal")!;

        var doc = new DocCommentReader().Read(animal)!;
        var md = new DocCommentMarkdownConverter().Convert(doc);

        Assert.Equal("A living creature in the sample domain.", md);
    }
    
}
