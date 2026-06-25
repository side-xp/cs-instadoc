using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Utility for pulling a symbol's documentation comment as parsed XML.
/// </summary>
/// <remarks>
/// Reads the doc comment Roslyn already built in memory (there is no <c>.xml</c> file). The result is the
/// <c>&lt;member&gt;</c> element whose <c>cref</c> attributes are resolved to documentation IDs (eg.
/// <c>T:Namespace.Type</c>, or <c>!:Name</c> when unresolvable), ready for the converter to turn into Markdown links.
/// Returns <see langword="null"/> when a symbol carries no documentation: undocumented members stay in the surface,
/// they simply have nothing to render here. <c>&lt;inheritdoc/&gt;</c> is returned verbatim by Roslyn and resolved
/// separately.
/// </remarks>
public sealed class DocCommentReader
{

    /// <summary>
    /// Reads and parses the documentation comment of a symbol.
    /// </summary>
    /// <param name="symbol">The symbol whose documentation to read (eg. a type or member from the API surface).</param>
    /// <param name="cancellationToken">Honored while Roslyn assembles the XML.</param>
    /// <returns>The parsed <c>&lt;member&gt;</c> element, or <see langword="null"/> if the symbol is undocumented.</returns>
    public XElement? Read(ISymbol symbol, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var xml = symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XElement.Parse(xml);
        }
        catch (XmlException)
        {
            // Tolerant by design: a malformed doc comment must degrade gracefully, not abort the run.
            return null;
        }
    }

}
