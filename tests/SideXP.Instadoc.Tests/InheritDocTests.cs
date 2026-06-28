using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <c>&lt;inheritdoc/&gt;</c> resolution (todo 1.1): a member documented only with <c>&lt;inheritdoc/&gt;</c>
/// should render the documentation of the member it overrides or the interface member it implements (one level up,
/// within the analyzed sources only) and fall back to a short note when no source for the docs can be found.
/// </summary>
/// <remarks>
/// The fixture <c>Fixtures/Inheritance/Members.cs</c> carries the scenarios:
/// <list type="bullet">
/// <item><c>Widget.Refresh</c> / <c>Widget.Name</c>: override a documented abstract base member.</item>
/// <item><c>PressureGauge.Reading</c> / <c>PressureGauge.Reset</c>: implement a documented interface member.</item>
/// <item><c>Widget.ToString</c>: overrides <c>object.ToString()</c> (no XML in our sources): the fallback case.</item>
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
        return new DocumentationRenderer().Render(surface, includeIndex: false, compilation: compilation);
    }

    private static string Page(IReadOnlyList<RenderedPage> pages, string relativePath)
        => pages.Single(page => page.RelativePath == relativePath).Content;

    /// <summary>The slice of a page covering one member: from its <c>### heading</c> to the next one (or the end).</summary>
    private static string Section(string page, string heading)
    {
        var start = page.IndexOf("### `" + heading + "`", StringComparison.Ordinal);
        Assert.True(start >= 0, $"section '{heading}' not found");
        var next = page.IndexOf("### ", start + 4, StringComparison.Ordinal);
        return next >= 0 ? page[start..next] : page[start..];
    }

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

    [Fact(DisplayName = "Inherits docs from a cref-named overload, keeping its own summary")]
    public void Inherits_from_cref_named_overload()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.Lookup.md");
        // Both overloads live on the same page, so scope to the inheriting one.
        var find = Section(page, "Find(int, string)");

        Assert.Contains("Finds an entry by its numeric id.", find);   // own summary
        Assert.DoesNotContain("Finds an entry by its key.", find);    // source summary not pulled in
        Assert.Contains("Returned when the key is absent.", find);    // inherited shared param
        Assert.Contains("The matching entry, or the fallback.", find); // inherited returns
    }

    [Fact(DisplayName = "An inherited param that the target does not declare is filtered out")]
    public void Cref_inheritance_filters_non_matching_params()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.Lookup.md");
        var find = Section(page, "Find(int, string)");

        // Find(int, string) has no `key` parameter, so the source's <param name="key"> must not leak in.
        Assert.DoesNotContain("The key to look up.", find);
    }

    [Fact(DisplayName = "Resolves a cref pointing at a private (non-surfaced) member")]
    public void Inherits_from_cref_to_private_member()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.Facade.md");

        // Facade.Run inherits from the private Compute(int): resolution goes through the source index, not the surface.
        Assert.Contains("Computes a result for the given input.", page);
        Assert.Contains("The value to process.", page);
        Assert.Contains("The computed result.", page);
    }

    [Fact(DisplayName = "Resolves a cref whose signature names an unresolvable type")]
    public void Resolves_cref_with_unresolvable_parameter_type()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.Registry.md");
        var register = Section(page, "Register(int, RegistrationOptions)");

        // The cref signature uses the undefined RegistrationOptions; matching by doc-id resolves it anyway.
        Assert.Contains("Registers by id.", register);            // own summary
        Assert.Contains("The registration options.", register);   // inherited shared param
        Assert.Contains("True on success.", register);            // inherited returns
        Assert.DoesNotContain("The registration name.", register); // source's `name` param filtered out
    }

    [Fact(DisplayName = "No fallback note when the member already has its own documentation")]
    public void No_fallback_note_when_member_has_own_docs()
    {
        var page = Page(RenderInheritanceFixture(), "Sample.Inheritance.Tag.md");

        // Tag.ToString has an unresolvable <inheritdoc/> but its own summary, so the note must be suppressed.
        Assert.Contains("Returns the tag's text.", page);
        Assert.DoesNotContain("Inherited documentation", page);
    }
}
