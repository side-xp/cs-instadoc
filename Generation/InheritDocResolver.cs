using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Utility that expands a top-level <c>&lt;inheritdoc/&gt;</c> in a member's documentation from another member's, within
/// the analyzed sources only. The source is the one named by an explicit <c>cref</c>, otherwise the member that this one
/// overrides or the interface member it implements.
/// </summary>
/// <remarks>
/// Tags the member declares itself win; the inherited tags fill the gaps, matched per key
/// (<c>param</c>/<c>typeparam</c> by <c>name</c>, <c>exception</c>/<c>seealso</c> by <c>cref</c>, and the singleton
/// sections like <c>summary</c> or <c>returns</c>) by tag. Inherited <c>param</c>/<c>typeparam</c> tags are kept only
/// when the target actually has a parameter of that name. A <c>cref</c> source may have a different signature, so its
/// extra parameters must not leak in.
/// When no source with concrete documentation is found (eg. the ancestor is a BCL or third-party type whose XML isn't
/// loaded here, or a base that itself only carries <c>&lt;inheritdoc/&gt;</c>), the tag is left in place and the
/// converter renders a short fallback note (only when the member has no documentation of its own). <c>path</c>-qualified
/// and nested <c>&lt;inheritdoc&gt;</c> are out of scope here (project todo §3.1).
/// </remarks>
public sealed class InheritDocResolver
{

    private readonly DocCommentReader _reader = new();

    /// <summary>
    /// Returns <paramref name="doc"/> with a top-level <c>&lt;inheritdoc/&gt;</c> expanded in place, or unchanged when
    /// there is none, or when none can be resolved to a documented own-source member.
    /// </summary>
    /// <param name="symbol">The symbol the documentation belongs to.</param>
    /// <param name="doc">The parsed <c>&lt;member&gt;</c> element (from <see cref="DocCommentReader"/>).</param>
    /// <param name="sourceMembers">Documentation-id → symbol index of the analyzed sources (from
    /// <see cref="IndexSourceMembers"/>), used to resolve a <c>cref</c>-named source; pass <see langword="null"/> to
    /// resolve only the override/interface source.</param>
    /// <param name="cancellationToken">Honored while reading the inherited symbol's documentation.</param>
    public XElement Resolve(
        ISymbol symbol,
        XElement doc,
        IReadOnlyDictionary<string, ISymbol>? sourceMembers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(doc);

        var inheritdoc = doc.Element("inheritdoc");
        if (inheritdoc is null)
        {
            return doc;
        }

        // An explicit cref names the source; otherwise inherit from the overridden/implemented member.
        var source = ResolveCref(inheritdoc, sourceMembers) ?? FindInheritanceSource(symbol);
        var sourceDoc = source is null ? null : _reader.Read(source, cancellationToken);

        // Only concrete tags can be inherited; an inherited <inheritdoc/> is not followed (no recursion in v1).
        var inherited = sourceDoc?
            .Elements()
            .Where(element => element.Name.LocalName != "inheritdoc")
            .ToList();

        if (inherited is not { Count: > 0 })
        {
            // No documented source: leave the <inheritdoc/> for the converter to render as a fallback note.
            return doc;
        }

        foreach (var element in inherited)
        {
            if (AppliesToTarget(symbol, element) && !DeclaresLocally(doc, element))
            {
                doc.Add(new XElement(element));
            }
        }

        // A documented source was found, so the tag is resolved even if every inherited tag was already present or
        // filtered out. Remove it so no spurious fallback note is produced.
        inheritdoc.Remove();
        return doc;
    }

    /// <summary>
    /// Resolves the member named by an <c>&lt;inheritdoc cref="..."/&gt;</c> to its symbol, or <see langword="null"/>
    /// when there is no (resolvable) cref or no source index to resolve it against.
    /// </summary>
    /// <remarks>
    /// Roslyn turns the cref into a documentation id (eg. <c>M:N.T.M(System.Int32)</c>), or <c>!:</c> when it couldn't
    /// be bound at all. Crucially, a parameter type that isn't in the reference set is written by its bare name (eg.
    /// <c>CodeNamespace</c>) (identical to how the *target* member's own <see cref="ISymbol.GetDocumentationCommentId"/>
    /// renders it). So matching the cref id against the source index resolves the reference without needing the type to
    /// be resolvable, which is what keeps this working for sources that reference assemblies the tool never loads.
    /// </remarks>
    private static ISymbol? ResolveCref(XElement inheritdoc, IReadOnlyDictionary<string, ISymbol>? sourceMembers)
    {
        var cref = inheritdoc.Attribute("cref")?.Value;
        if (sourceMembers is null || string.IsNullOrEmpty(cref))
        {
            return null;
        }

        return sourceMembers.TryGetValue(cref, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Indexes every type and member declared in <paramref name="compilation"/>'s sources by its documentation comment
    /// id, so a <c>cref</c> can be resolved by matching ids (see <see cref="ResolveCref"/>). Members defined only in
    /// referenced metadata are skipped (they have no XML to inherit here anyway).
    /// </summary>
    public static IReadOnlyDictionary<string, ISymbol> IndexSourceMembers(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var index = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(compilation.GlobalNamespace);

        while (pending.Count > 0)
        {
            foreach (var member in pending.Pop().GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol @namespace:
                        pending.Push(@namespace);
                        break;

                    // Only types declared in source can contribute inheritable docs; metadata types are skipped.
                    case INamedTypeSymbol type when !type.DeclaringSyntaxReferences.IsEmpty:
                        Add(index, type);
                        pending.Push(type);
                        break;

                    case not INamedTypeSymbol:
                        Add(index, member);
                        break;
                }
            }
        }

        return index;

        static void Add(Dictionary<string, ISymbol> index, ISymbol symbol)
        {
            if (symbol.GetDocumentationCommentId() is { } id)
            {
                index.TryAdd(id, symbol);
            }
        }
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

    /// <summary>
    /// True when an inherited tag applies to the target member. A <c>cref</c> source can have a different signature, so
    /// its <c>param</c>/<c>typeparam</c> tags are kept only for parameters the target actually declares; every other tag
    /// applies regardless.
    /// </summary>
    private static bool AppliesToTarget(ISymbol symbol, XElement inherited) => inherited.Name.LocalName switch
    {
        "param" => Parameters(symbol).Any(parameter => parameter.Name == inherited.Attribute("name")?.Value),
        "typeparam" => TypeParameters(symbol).Any(parameter => parameter.Name == inherited.Attribute("name")?.Value),
        _ => true,
    };

    private static ImmutableArray<IParameterSymbol> Parameters(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.Parameters,
        IPropertySymbol property => property.Parameters, // indexers
        _ => ImmutableArray<IParameterSymbol>.Empty,
    };

    private static ImmutableArray<ITypeParameterSymbol> TypeParameters(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.TypeParameters,
        INamedTypeSymbol type => type.TypeParameters,
        _ => ImmutableArray<ITypeParameterSymbol>.Empty,
    };

}
