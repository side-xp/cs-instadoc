using Microsoft.CodeAnalysis;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Represents a single type selected for documentation, together with the members of it that passed the visibility
/// filter.
/// </summary>
/// <remarks>
/// Produced by <see cref="ApiSurfaceExtractor"/> (one instance per type that will become a Markdown page) and consumed
/// by the later rendering stages. Members are the type's own methods, properties, fields and events; nested types are
/// not listed here (each becomes its own <see cref="DocumentedType"/>).
/// </remarks>
public sealed record DocumentedType
{

    /// <summary>
    /// The type symbol itself (class, struct, interface, enum, delegate, …).
    /// </summary>
    public required INamedTypeSymbol Symbol { get; init; }

    /// <summary>
    /// The selected members of <see cref="Symbol"/>, in declaration order, already filtered by visibility.
    /// </summary>
    public required IReadOnlyList<ISymbol> Members { get; init; }

}
