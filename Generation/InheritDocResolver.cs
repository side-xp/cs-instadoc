using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Utility that expands a top-level <c>&lt;inheritdoc/&gt;</c> in a member's documentation from the member it
/// overrides or the interface member it implements, within the analyzed sources only.
/// </summary>
/// <remarks>
/// Tags the member declares itself win; the inherited tags fill the gaps, matched per key
/// (<c>param</c>/<c>typeparam</c> by <c>name</c>, <c>exception</c>/<c>seealso</c> by <c>cref</c>, and the singleton
/// sections like <c>summary</c> or <c>returns</c>) by tag.
/// When no source with concrete documentation is found (eg. the ancestor is a BCL or third-party type whose XML isn't
/// loaded here, or a base that itself only carries <c>&lt;inheritdoc/&gt;</c>), the tag is left in place and the
/// converter renders a short fallback note.
/// </remarks>
public sealed class InheritDocResolver
{

    private readonly DocCommentReader _reader = new();

    /// <summary>
    /// Returns <paramref name="doc"/> with a top-level <c>&lt;inheritdoc/&gt;</c> expanded in place, or unchanged when
    /// there is none — or when none can be resolved to documented own-source ancestor.
    /// </summary>
    /// <param name="symbol">The symbol the documentation belongs to.</param>
    /// <param name="doc">The parsed <c>&lt;member&gt;</c> element (from <see cref="DocCommentReader"/>).</param>
    /// <param name="cancellationToken">Honored while reading the inherited symbol's documentation.</param>
    public XElement Resolve(ISymbol symbol, XElement doc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(doc);

        var inheritdoc = doc.Element("inheritdoc");
        if (inheritdoc is null)
        {
            return doc;
        }

        var source = FindInheritanceSource(symbol);
        var sourceDoc = source is null ? null : _reader.Read(source, cancellationToken);

        // Only concrete tags can be inherited; an inherited <inheritdoc/> is not followed (no recursion in v1).
        var inherited = sourceDoc?
            .Elements()
            .Where(element => element.Name.LocalName != "inheritdoc")
            .ToList();

        if (inherited is not { Count: > 0 })
        {
            // Nothing concrete to inherit: leave the <inheritdoc/> for the converter to render as a fallback note.
            return doc;
        }

        foreach (var element in inherited)
        {
            if (!DeclaresLocally(doc, element))
            {
                doc.Add(new XElement(element));
            }
        }

        inheritdoc.Remove();
        return doc;
    }

    /// <summary>
    /// Finds the member whose documentation an overriding/implementing member inherits: the overridden base member
    /// first, otherwise the implemented interface member. Returns <see langword="null"/> when the member neither
    /// overrides nor implements anything.
    /// </summary>
    private static ISymbol? FindInheritanceSource(ISymbol symbol)
    {
        var overridden = symbol switch
        {
            IMethodSymbol method => (ISymbol?)method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null,
        };

        return overridden ?? FindImplementedInterfaceMember(symbol);
    }

    /// <summary>
    /// Returns the interface member that <paramref name="symbol"/> implements (implicitly or explicitly), or
    /// <see langword="null"/> if it implements none.
    /// </summary>
    private static ISymbol? FindImplementedInterfaceMember(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        if (containingType is null)
        {
            return null;
        }

        foreach (var @interface in containingType.AllInterfaces)
        {
            foreach (var member in @interface.GetMembers())
            {
                var implementation = containingType.FindImplementationForInterfaceMember(member);
                if (implementation is not null && SymbolEqualityComparer.Default.Equals(implementation, symbol))
                {
                    return member;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// True when the member already declares its own version of an inherited tag, so the local one wins. Matched by the
    /// tag's identity: <c>param</c>/<c>typeparam</c> by <c>name</c>, <c>exception</c>/<c>seealso</c> by <c>cref</c>,
    /// every other (singleton) section by tag name.
    /// </summary>
    private static bool DeclaresLocally(XElement doc, XElement inherited)
    {
        var name = inherited.Name.LocalName;

        var key = name switch
        {
            "param" or "typeparam" => "name",
            "exception" or "seealso" => "cref",
            _ => null,
        };

        if (key is null)
        {
            return doc.Element(name) is not null;
        }

        var inheritedKey = inherited.Attribute(key)?.Value;
        return doc.Elements(name).Any(local => local.Attribute(key)?.Value == inheritedKey);
    }

}
