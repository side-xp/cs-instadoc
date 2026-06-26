using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <c>&lt;inheritdoc/&gt;</c> resolution (todo 1.1): a member documented only with <c>&lt;inheritdoc/&gt;</c>
/// should render the documentation of the member it overrides or the interface member it implements — one level up,
/// within the analyzed sources only — and fall back to a short note when no source for the docs can be found.
/// </summary>
/// <remarks>
/// The fixture <c>Fixtures/Inheritance/Members.cs</c> carries the scenarios:
/// <list type="bullet">
/// <item><c>Widget.Refresh</c> / <c>Widget.Name</c> — override a documented abstract base member.</item>
/// <item><c>PressureGauge.Reading</c> / <c>PressureGauge.Reset</c> — implement a documented interface member.</item>
/// <item><c>Widget.ToString</c> — overrides <c>object.ToString()</c> (no XML in our sources): the fallback case.</item>
/// </list>
/// The assertions are made on the rendered page, so they describe the user-visible outcome rather than any particular
/// resolver API.
/// </remarks>
public class InheritDocTests
{
    private static string InheritanceFile
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Inheritance", "Members.cs");

    private static IReadOnlyList<RenderedPage> RenderInheritanceFixture()
    {
        var trees = new SourceParser().Parse([InheritanceFile]);
        var compilation = new CompilationBuilder().Build(trees);
        var surface = new ApiSurfaceExtractor().Extract(compilation, ["public", "protected"]);
        return new DocumentationRenderer().Render(surface, includeIndex: false);
    }

    private static string Page(IReadOnlyList<RenderedPage> pages, string relativePath)
        => pages.Single(page => page.RelativePath == relativePath).Content;

    [Fact(DisplayName = "Inherits docs from an overridden base method")]
    public void Inherits_from_overridden_method()
    {
        var widget = Page(RenderInheritanceFixture(), "Sample.Inheritance.Widget.md");

        Assert.Contains("Refreshes the widget's state from its source.", widget);
        Assert.Contains("refresh even if nothing changed", widget);   // inherited <param>
        Assert.Contains("number of fields that were updated", widget); // inherited <returns>
    }

    [Fact(DisplayName = "Inherits docs from an overridden base property")]
    public void Inherits_from_overridden_property()
    {
        var widget = Page(RenderInheritanceFixture(), "Sample.Inheritance.Widget.md");

        Assert.Contains("human-readable display name", widget);
    }

    [Fact(DisplayName = "Inherits docs from an implemented interface method")]
    public void Inherits_from_implemented_interface_method()
    {
        var gauge = Page(RenderInheritanceFixture(), "Sample.Inheritance.PressureGauge.md");

        Assert.Contains("Resets the gauge back to zero.", gauge);
    }

    [Fact(DisplayName = "Inherits docs from an implemented interface property")]
    public void Inherits_from_implemented_interface_property()
    {
        var gauge = Page(RenderInheritanceFixture(), "Sample.Inheritance.PressureGauge.md");

        Assert.Contains("current reading, in bars", gauge);
    }

    [Fact(DisplayName = "Falls back to a note when the inherited source is external")]
    public void Falls_back_when_source_is_external()
    {
        // Widget.ToString overrides object.ToString(), whose XML is not in our sources: no docs to inherit.
        var widget = Page(RenderInheritanceFixture(), "Sample.Inheritance.Widget.md");

        // A short, non-empty note rather than a blank body (exact wording to be confirmed in the plan).
        Assert.Contains("Inherited documentation", widget);
    }

    [Fact(DisplayName = "A member's own docs are not affected by inheritdoc resolution")]
    public void Local_docs_are_preserved()
    {
        // Widget.Id has its own summary and no <inheritdoc/>; resolution must leave it untouched.
        var widget = Page(RenderInheritanceFixture(), "Sample.Inheritance.Widget.md");

        Assert.Contains("The widget's identifier.", widget);
    }

    // -- Partial inheritance: local tags win per key, the rest is inherited -----------------------

    [Fact(DisplayName = "Re-documenting one parameter still inherits the summary, other params and returns")]
    public void Partial_redocuments_one_parameter()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.RewriteCommand.md");

        Assert.Contains("The absolute path this command rewrites in place.", page); // local param wins
        Assert.Contains("Runs the command against the given target.", page);        // inherited summary
        Assert.Contains("How many times to retry on failure.", page);              // inherited other param
        Assert.Contains("The process exit code.", page);                            // inherited returns
        // The base's text for the re-documented parameter must NOT also appear.
        Assert.DoesNotContain("The path the command operates on.", page);
    }

    [Fact(DisplayName = "Overriding only the summary inherits both params and the return value")]
    public void Partial_overrides_summary_only()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.QuietCommand.md");

        Assert.Contains("Runs the command without writing progress to the console.", page); // local summary wins
        Assert.DoesNotContain("Runs the command against the given target.", page);          // base summary dropped
        Assert.Contains("The path the command operates on.", page);                         // inherited param
        Assert.Contains("How many times to retry on failure.", page);                       // inherited param
        Assert.Contains("The process exit code.", page);                                     // inherited returns
    }

    [Fact(DisplayName = "Overriding only the return value inherits the summary and both params")]
    public void Partial_overrides_returns_only()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.CountingCommand.md");

        Assert.Contains("The number of files that were changed.", page); // local returns wins
        Assert.DoesNotContain("The process exit code.", page);           // base returns dropped
        Assert.Contains("Runs the command against the given target.", page); // inherited summary
        Assert.Contains("The path the command operates on.", page);          // inherited param
        Assert.Contains("How many times to retry on failure.", page);        // inherited param
    }
}
