using Microsoft.CodeAnalysis;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Utility for walking the compilation's own symbols and selects the types and members to document.
/// </summary>
/// <remarks>
/// Only types declared in the analyzed sources are considered (the BCL and other references are skipped). Selection is
/// driven by the requested visibility levels, and works from the <em>symbol model</em> rather than the compiler's XML
/// documentation (so undocumented members still appear, because an API reference should show the whole surface, not
/// just the parts someone remembered to comment).
/// </remarks>
public sealed class ApiSurfaceExtractor
{

    /// <summary>
    /// Selects the documentable types and members from the compilation.
    /// </summary>
    /// <param name="compilation">A built compilation (eg. from <see cref="CompilationBuilder"/>).</param>
    /// <param name="visibility">Visibility levels to include, eg. <c>public</c>, <c>protected</c>.</param>
    /// <param name="cancellationToken">Honored while walking the symbol tree.</param>
    /// <returns>One <see cref="DocumentedType"/> per selected type, in symbol-traversal order.</returns>
    public IReadOnlyList<DocumentedType> Extract(
        Compilation compilation,
        IReadOnlyList<string> visibility,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(visibility);

        // Refine accessibility levels
        var allowed = ParseVisibility(visibility);
        var results = new List<DocumentedType>();

        // compilation.Assembly.GlobalNamespace contains only the types declared in the analyzed sources,
        // never the referenced BCL types.
        foreach (var type in EnumerateTypes(compilation.Assembly.GlobalNamespace, cancellationToken))
        {
            if (!IsIncluded(type, allowed))
            {
                continue;
            }

            var members = type.GetMembers()
                .Where(member => IsDocumentableMember(member) && IsIncluded(member, allowed))
                .ToList();

            results.Add(new DocumentedType { Symbol = type, Members = members });
        }

        return results;
    }

    /// <summary>
    /// Yields every named type under a namespace, descending into child namespaces and into nested types.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns, CancellationToken cancellationToken)
    {
        foreach (var member in ns.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (member)
            {
                case INamespaceSymbol childNamespace:
                    foreach (var nested in EnumerateTypes(childNamespace, cancellationToken))
                    {
                        yield return nested;
                    }
                    break;

                case INamedTypeSymbol type:
                    foreach (var nested in EnumerateTypeAndNested(type))
                    {
                        yield return nested;
                    }
                    break;
            }
        }
    }

    /// <summary>Yields a type followed by each of its nested types, recursively.</summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateTypeAndNested(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var descendant in EnumerateTypeAndNested(nested))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// A symbol is included when it is explicitly declared (no compiler-generated members) and its own declared
    /// accessibility is one of the requested levels.
    /// </summary>
    private static bool IsIncluded(ISymbol symbol, ISet<Accessibility> allowed)
        => !symbol.IsImplicitlyDeclared && allowed.Contains(symbol.DeclaredAccessibility);

    /// <summary>
    /// True for the member kinds rendered on a type's page. Nested types are excluded (each is its own page), and
    /// property/event accessor methods (<c>get_</c>/<c>set_</c>/<c>add_</c>/<c>remove_</c>) are excluded as noise.
    /// </summary>
    private static bool IsDocumentableMember(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol => false,
            IMethodSymbol method => method.AssociatedSymbol is null,
            IPropertySymbol or IFieldSymbol or IEventSymbol => true,
            _ => false,
        };
    }

    /// <summary>
    /// Maps the textual visibility levels onto the Roslyn <see cref="Accessibility"/> values they cover.
    /// </summary>
    private static HashSet<Accessibility> ParseVisibility(IReadOnlyList<string> visibility)
    {
        var allowed = new HashSet<Accessibility>();
        foreach (var level in visibility)
        {
            switch (level.Trim().ToLowerInvariant())
            {
                case "public":
                    allowed.Add(Accessibility.Public);
                    break;
                case "protected":
                    allowed.Add(Accessibility.Protected);
                    // "protected internal" is part of the inheritance surface, so it counts as protected.
                    allowed.Add(Accessibility.ProtectedOrInternal);
                    break;
                case "internal":
                    allowed.Add(Accessibility.Internal);
                    break;
                case "private":
                    allowed.Add(Accessibility.Private);
                    allowed.Add(Accessibility.ProtectedAndInternal); // "private protected"
                    break;
            }
        }

        return allowed;
    }

}
