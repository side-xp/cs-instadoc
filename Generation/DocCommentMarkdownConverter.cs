using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Step 6 of the pipeline: converts a symbol's documentation comment (the <c>&lt;member&gt;</c> element from
/// <see cref="DocCommentReader"/>) into GitHub Flavored Markdown.
/// </summary>
/// <remarks>
/// Each documentation tag maps to a Markdown construct: <c>summary</c> is the lead prose, <c>param</c>/<c>typeparam</c>
/// become labelled lists, <c>returns</c>/<c>value</c>/<c>remarks</c>/<c>example</c> become labelled sections,
/// <c>exception</c> and <c>seealso</c> become their own lists, and inline tags (<c>see</c>, <c>paramref</c>, <c>c</c>,
/// …) render inline. Cross-references go through an injected <see cref="CrefResolver"/> so this stage stays unaware of
/// the page layout.
/// </remarks>
public sealed partial class DocCommentMarkdownConverter
{

    private readonly CrefResolver _resolveCref;

    /// <summary>
    /// Creates a converter.
    /// </summary>
    /// <param name="crefResolver">
    /// How to render cross-references; defaults to <see cref="DefaultCrefResolver"/> (inline code, no links) when
    /// omitted.
    /// </param>
    public DocCommentMarkdownConverter(CrefResolver? crefResolver = null)
        => _resolveCref = crefResolver ?? DefaultCrefResolver;

    /// <summary>
    /// Converts a documentation <c>&lt;member&gt;</c> element to Markdown.
    /// </summary>
    /// <param name="member">The parsed documentation element for a single symbol.</param>
    /// <returns>GitHub Flavored Markdown for the documentation body (no trailing whitespace).</returns>
    public string Convert(XElement member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var sb = new StringBuilder();

        AppendSection(sb, member.Element("summary"), heading: null);
        AppendNamedList(sb, member.Elements("typeparam"), "Type parameters");
        AppendNamedList(sb, member.Elements("param"), "Parameters");
        AppendSection(sb, member.Element("returns"), "Returns");
        AppendSection(sb, member.Element("value"), "Value");
        AppendCrefList(sb, member.Elements("exception"), "Exceptions");
        AppendSection(sb, member.Element("example"), "Example");
        AppendSection(sb, member.Element("remarks"), "Remarks");
        AppendCrefList(sb, member.Elements("seealso"), "See also");

        var body = sb.ToString().Trim();

        // A residual top-level <inheritdoc/> is one the resolver could not expand. Only note it when the member has no
        // documentation of its own — otherwise the note is just noise on top of real content.
        if (body.Length == 0 && member.Element("inheritdoc") is not null)
        {
            return "*Inherited documentation.*";
        }

        return body;
    }

    /// <summary>
    /// Appends a labelled (or, when <paramref name="heading"/> is null, unlabelled) block section.
    /// </summary>
    private void AppendSection(StringBuilder sb, XElement? element, string? heading)
    {
        if (element is null)
        {
            return;
        }

        var content = RenderBlocks(element);
        if (content.Length == 0)
        {
            return;
        }

        if (heading is not null)
        {
            sb.Append("**").Append(heading).Append("**\n\n");
        }

        sb.Append(content).Append("\n\n");
    }

    /// <summary>
    /// Appends a list of named entries (<c>param</c>/<c>typeparam</c>): <c>- `name`: description</c>.
    /// </summary>
    private void AppendNamedList(StringBuilder sb, IEnumerable<XElement> elements, string heading)
    {
        var items = elements.ToList();
        if (items.Count == 0)
        {
            return;
        }

        sb.Append("**").Append(heading).Append("**\n\n");
        foreach (var item in items)
        {
            var name = item.Attribute("name")?.Value ?? string.Empty;
            var description = NormalizeInline(RenderInline(item.Nodes()));

            sb.Append("- `").Append(name).Append('`');
            if (description.Length > 0)
            {
                sb.Append(": ").Append(description);
            }
            sb.Append('\n');
        }
        sb.Append('\n');
    }

    /// <summary>
    /// Appends a list whose entries lead with a cref (<c>exception</c>/<c>seealso</c>).
    /// </summary>
    private void AppendCrefList(StringBuilder sb, IEnumerable<XElement> elements, string heading)
    {
        var items = elements.ToList();
        if (items.Count == 0)
        {
            return;
        }

        sb.Append("**").Append(heading).Append("**\n\n");
        foreach (var item in items)
        {
            var cref = item.Attribute("cref")?.Value;
            var reference = cref is not null ? _resolveCref(cref, null) : string.Empty;
            var description = NormalizeInline(RenderInline(item.Nodes()));

            sb.Append("- ").Append(reference);
            if (description.Length > 0)
            {
                sb.Append(": ").Append(description);
            }
            sb.Append('\n');
        }
        sb.Append('\n');
    }

    /// <summary>
    /// Renders an element's flow content: paragraphs, code blocks and lists, with inline runs between them.
    /// </summary>
    private string RenderBlocks(XElement element)
    {
        var blocks = new List<string>();
        var paragraph = new StringBuilder();

        void FlushParagraph()
        {
            var text = NormalizeInline(paragraph.ToString());
            if (text.Length > 0)
            {
                blocks.Add(text);
            }
            paragraph.Clear();
        }

        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    paragraph.Append(EscapeMarkdown(text.Value));
                    break;

                case XElement { Name.LocalName: "para" } para:
                    FlushParagraph();
                    blocks.Add(NormalizeInline(RenderInline(para.Nodes())));
                    break;

                case XElement { Name.LocalName: "code" } code:
                    FlushParagraph();
                    blocks.Add(RenderCodeBlock(code));
                    break;

                case XElement { Name.LocalName: "list" } list:
                    FlushParagraph();
                    blocks.Add(RenderList(list));
                    break;

                case XElement inline:
                    paragraph.Append(RenderInlineElement(inline));
                    break;
            }
        }

        FlushParagraph();
        return string.Join("\n\n", blocks.Where(block => block.Length > 0));
    }

    /// <summary>
    /// Renders mixed inline content (text plus inline tags) into a single Markdown run.
    /// </summary>
    private string RenderInline(IEnumerable<XNode> nodes)
    {
        var sb = new StringBuilder();
        foreach (var node in nodes)
        {
            switch (node)
            {
                case XText text:
                    sb.Append(EscapeMarkdown(text.Value));
                    break;
                case XElement element:
                    sb.Append(RenderInlineElement(element));
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Renders a single inline tag (<c>see</c>, <c>paramref</c>, <c>c</c>, …).
    /// </summary>
    private string RenderInlineElement(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
                return RenderReference(element);

            case "paramref":
            case "typeparamref":
                return $"`{element.Attribute("name")?.Value}`";

            case "c":
                return $"`{element.Value.Trim()}`";

            default:
                // Unknown inline tag: fall back to its inner content so nothing is lost.
                return RenderInline(element.Nodes());
        }
    }

    /// <summary>
    /// Renders a <c>see</c>/<c>seealso</c>: a langword keyword, a cref (via the resolver), or an href link.
    /// </summary>
    private string RenderReference(XElement element)
    {
        var langword = element.Attribute("langword")?.Value;
        if (langword is not null)
        {
            return $"`{langword}`";
        }

        var cref = element.Attribute("cref")?.Value;
        if (cref is not null)
        {
            var display = NormalizeInline(RenderInline(element.Nodes()));
            return _resolveCref(cref, display.Length > 0 ? display : null);
        }

        var href = element.Attribute("href")?.Value;
        if (href is not null)
        {
            var text = NormalizeInline(RenderInline(element.Nodes()));
            return text.Length > 0 ? $"[{text}]({href})" : href;
        }

        return string.Empty;
    }

    /// <summary>
    /// Renders a <c>&lt;code&gt;</c> block as a fenced GFM code block, de-indented.
    /// </summary>
    private static string RenderCodeBlock(XElement element)
    {
        var code = Dedent(element.Value).Trim('\n');
        var language = element.Attribute("language")?.Value ?? "csharp";
        return $"```{language}\n{code}\n```";
    }

    /// <summary>
    /// Renders a <c>&lt;list&gt;</c> as a bullet or numbered Markdown list.
    /// </summary>
    private string RenderList(XElement element)
    {
        var ordered = element.Attribute("type")?.Value == "number";
        var sb = new StringBuilder();
        var index = 1;

        foreach (var item in element.Elements("item"))
        {
            var term = item.Element("term");
            var description = item.Element("description");

            string text;
            if (term is not null && description is not null)
            {
                text = $"**{NormalizeInline(RenderInline(term.Nodes()))}**: {NormalizeInline(RenderInline(description.Nodes()))}";
            }
            else
            {
                var body = description ?? item;
                text = NormalizeInline(RenderInline(body.Nodes()));
            }

            sb.Append(ordered ? $"{index}. {text}" : $"- {text}").Append('\n');
            index++;
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Collapses runs of whitespace (including the newlines and indentation from <c>///</c>) to single spaces.
    /// </summary>
    private static string NormalizeInline(string text) => WhitespaceRun().Replace(text, " ").Trim();

    /// <summary>
    /// Escapes Markdown metacharacters in a plain-text run so they render literally.
    /// Only applied to raw text nodes; content already inside code spans or fenced blocks is not passed here.
    /// </summary>
    private static string EscapeMarkdown(string text) => MarkdownMetaChar().Replace(text, m => @"\" + m.Value);

    /// <summary>
    /// Removes the common leading indentation shared by every non-blank line of a code block.
    /// </summary>
    private static string Dedent(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var indents = lines
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.Length - line.TrimStart().Length)
            .ToList();

        var common = indents.Count > 0 ? indents.Min() : 0;
        return string.Join('\n', lines.Select(line => line.Length >= common ? line[common..] : line));
    }

    /// <summary>
    /// The default cross-reference renderer: inline code of the referenced member's simple name, with no link (the
    /// rendering stage replaces this with a page-aware resolver).
    /// </summary>
    public static string DefaultCrefResolver(string crefId, string? displayText)
        => $"`{displayText ?? SimpleNameFromId(crefId)}`";

    /// <summary>
    /// Extracts the trailing simple name from a documentation ID (eg. <c>M:N.T.M(System.Int32)</c> → <c>M</c>).
    /// </summary>
    internal static string SimpleNameFromId(string crefId)
    {
        var id = crefId;

        var colon = id.IndexOf(':');
        if (colon >= 0)
        {
            id = id[(colon + 1)..];
        }

        var paren = id.IndexOf('(');
        if (paren >= 0)
        {
            id = id[..paren];
        }

        var dot = id.LastIndexOf('.');
        return dot >= 0 ? id[(dot + 1)..] : id;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"[*_\[]")]
    private static partial Regex MarkdownMetaChar();

}
