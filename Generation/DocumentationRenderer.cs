using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Step 7 of the pipeline: renders the selected API surface into Markdown pages — one per type, plus an optional index.
/// </summary>
/// <remarks>
/// Rendering happens in two passes. First a map from every documented symbol's doc-comment ID to its page (and member
/// anchor) is built; that map backs the <see cref="CrefResolver"/> so cross-references become real links. Then each
/// type is rendered to a page, pulling and converting doc comments per symbol (via <see cref="DocCommentReader"/> and
/// <see cref="DocCommentMarkdownConverter"/>). The renderer produces <see cref="RenderedPage"/> values and performs no
/// file I/O of its own.
/// </remarks>
public sealed partial class DocumentationRenderer
{

    private static readonly SymbolDisplayFormat NameWithGenerics = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    private static readonly SymbolDisplayFormat TypeReference = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat MemberTitle = new(
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat MemberSignature = new(
        memberOptions: SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeAccessibility
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeConstantValue,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeDefaultValue,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly (string Title, Func<ISymbol, bool> Predicate)[] MemberGroups =
    [
        ("Constructors", symbol => symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }),
        ("Fields", symbol => symbol is IFieldSymbol),
        ("Properties", symbol => symbol is IPropertySymbol),
        ("Events", symbol => symbol is IEventSymbol),
        ("Methods", symbol => symbol is IMethodSymbol { MethodKind: not MethodKind.Constructor }),
    ];

    private readonly DocCommentReader _reader = new();

    /// <summary>
    /// Renders the API surface into Markdown pages.
    /// </summary>
    /// <param name="surface">The selected types and members (eg. from <see cref="ApiSurfaceExtractor"/>).</param>
    /// <param name="includeIndex">When <see langword="true"/>, also produce an <c>index.md</c> page.</param>
    /// <param name="cancellationToken">Honored between pages.</param>
    /// <returns>One page per type, plus the index page when requested.</returns>
    public IReadOnlyList<RenderedPage> Render(
        IReadOnlyList<DocumentedType> surface,
        bool includeIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var pageMap = BuildPageMap(surface);
        var converter = new DocCommentMarkdownConverter(CreateResolver(pageMap));

        var pages = new List<RenderedPage>(surface.Count + 1);
        foreach (var type in surface)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.Add(new RenderedPage
            {
                RelativePath = FileNameFor(type.Symbol),
                Content = RenderType(type, converter),
            });
        }

        if (includeIndex)
        {
            pages.Add(new RenderedPage { RelativePath = "index.md", Content = RenderIndex(surface) });
        }

        return pages;
    }

    /// <summary>
    /// Maps each documented type and member doc-comment ID to its page (and member anchor).
    /// </summary>
    private static Dictionary<string, string> BuildPageMap(IReadOnlyList<DocumentedType> surface)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in surface)
        {
            var file = FileNameFor(type.Symbol);
            var typeId = type.Symbol.GetDocumentationCommentId();
            if (typeId is not null)
            {
                map[typeId] = file;
            }

            foreach (var member in type.Members)
            {
                var memberId = member.GetDocumentationCommentId();
                if (memberId is not null)
                {
                    map[memberId] = $"{file}#{AnchorFor(member)}";
                }
            }
        }

        return map;
    }

    /// <summary>
    /// A cref resolver that links to documented pages, falling back to inline code for anything unmapped.
    /// </summary>
    private static CrefResolver CreateResolver(IReadOnlyDictionary<string, string> pageMap)
        => (crefId, displayText) => pageMap.TryGetValue(crefId, out var link)
            ? $"[{displayText ?? DocCommentMarkdownConverter.SimpleNameFromId(crefId)}]({link})"
            : DocCommentMarkdownConverter.DefaultCrefResolver(crefId, displayText);

    /// <summary>
    /// Renders one type's page: header, signature, type docs, then grouped members.
    /// </summary>
    private string RenderType(DocumentedType type, DocCommentMarkdownConverter converter)
    {
        var sb = new StringBuilder();

        sb.Append("# ").Append(type.Symbol.ToDisplayString(NameWithGenerics)).Append("\n\n");

        if (!type.Symbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.Append("Namespace: `").Append(type.Symbol.ContainingNamespace.ToDisplayString()).Append("`\n\n");
        }

        sb.Append("```csharp\n").Append(TypeSignature(type.Symbol)).Append("\n```\n\n");

        AppendDoc(sb, type.Symbol, converter);

        foreach (var (title, predicate) in MemberGroups)
        {
            var members = type.Members.Where(predicate).ToList();
            if (members.Count == 0)
            {
                continue;
            }

            sb.Append("## ").Append(title).Append("\n\n");
            foreach (var member in members)
            {
                sb.Append("### ").Append(member.ToDisplayString(MemberTitle)).Append("\n\n");
                sb.Append("<a id=\"").Append(AnchorFor(member)).Append("\"></a>\n\n");
                sb.Append("```csharp\n").Append(member.ToDisplayString(MemberSignature)).Append("\n```\n\n");
                AppendDoc(sb, member, converter);
            }
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// Appends a symbol's converted documentation body, if it has any.
    /// </summary>
    private void AppendDoc(StringBuilder sb, ISymbol symbol, DocCommentMarkdownConverter converter)
    {
        var doc = _reader.Read(symbol);
        if (doc is null)
        {
            return;
        }

        var body = converter.Convert(doc);
        if (body.Length > 0)
        {
            sb.Append(body).Append("\n\n");
        }
    }

    /// <summary>
    /// Renders the index page: every documented type, grouped by namespace and linked to its page.
    /// </summary>
    private static string RenderIndex(IReadOnlyList<DocumentedType> surface)
    {
        var sb = new StringBuilder();
        sb.Append("# API Reference\n\n");

        var byNamespace = surface
            .GroupBy(type => type.Symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : type.Symbol.ContainingNamespace.ToDisplayString())
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in byNamespace)
        {
            if (group.Key.Length > 0)
            {
                sb.Append("## ").Append(group.Key).Append("\n\n");
            }

            foreach (var type in group.OrderBy(type => type.Symbol.Name, StringComparer.Ordinal))
            {
                sb.Append("- [")
                    .Append(type.Symbol.ToDisplayString(NameWithGenerics))
                    .Append("](")
                    .Append(FileNameFor(type.Symbol))
                    .Append(")\n");
            }
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// Builds a readable declaration line for a type (accessibility, modifiers, kind, name, base list).
    /// </summary>
    private static string TypeSignature(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Delegate && type.DelegateInvokeMethod is { } invoke)
        {
            var parameters = string.Join(", ", invoke.Parameters.Select(p => p.ToDisplayString(MemberSignature)));
            return $"{AccessibilityKeyword(type.DeclaredAccessibility)} delegate "
                + $"{invoke.ReturnType.ToDisplayString(TypeReference)} {type.ToDisplayString(NameWithGenerics)}({parameters})";
        }

        var parts = new List<string> { AccessibilityKeyword(type.DeclaredAccessibility) };
        if (type.IsStatic)
        {
            parts.Add("static");
        }
        if (type.IsAbstract && type.TypeKind == TypeKind.Class)
        {
            parts.Add("abstract");
        }
        if (type.IsSealed && type.TypeKind == TypeKind.Class && !type.IsStatic)
        {
            parts.Add("sealed");
        }
        parts.Add(TypeKindKeyword(type));
        parts.Add(type.ToDisplayString(NameWithGenerics));

        var signature = string.Join(' ', parts.Where(part => part.Length > 0));

        var baseList = new List<string>();
        if (type.TypeKind == TypeKind.Class
            && type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            baseList.Add(baseType.ToDisplayString(TypeReference));
        }
        baseList.AddRange(type.Interfaces.Select(i => i.ToDisplayString(TypeReference)));

        return baseList.Count > 0 ? $"{signature} : {string.Join(", ", baseList)}" : signature;
    }

    private static string TypeKindKeyword(INamedTypeSymbol type) => type switch
    {
        { TypeKind: TypeKind.Interface } => "interface",
        { TypeKind: TypeKind.Enum } => "enum",
        { TypeKind: TypeKind.Struct, IsRecord: true } => "record struct",
        { TypeKind: TypeKind.Struct } => "struct",
        { IsRecord: true } => "record",
        _ => "class",
    };

    private static string AccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        _ => string.Empty,
    };

    /// <summary>The Markdown file name for a type, derived from its doc-comment ID (eg. <c>Sample.Shapes.Circle.md</c>).</summary>
    private static string FileNameFor(INamedTypeSymbol type)
    {
        var id = type.GetDocumentationCommentId() ?? $"T:{type.Name}";
        var name = id.StartsWith("T:", StringComparison.Ordinal) ? id[2..] : id;
        return name.Replace('`', '-') + ".md";
    }

    /// <summary>A stable, per-symbol anchor slug (eg. <c>Add(int, int)</c> → <c>add-int-int</c>).</summary>
    private static string AnchorFor(ISymbol member) => Slugify(member.ToDisplayString(MemberTitle));

    private static string Slugify(string text) => NonSlugCharacters().Replace(text.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

}
