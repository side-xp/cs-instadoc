using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Step 7 of the pipeline: renders the selected API surface into Markdown pages (one per type, plus an optional index).
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
    private readonly InheritDocResolver _inheritDoc = new();

    /// <summary>
    /// Renders the API surface into Markdown pages.
    /// </summary>
    /// <param name="surface">The selected types and members (eg. from <see cref="ApiSurfaceExtractor"/>).</param>
    /// <param name="includeIndex">When <see langword="true"/>, also produce an <c>index.md</c> page.</param>
    /// <param name="grouping">How type pages are laid out under the output folder (see <see cref="Grouping"/>).</param>
    /// <param name="compilation">The compilation the surface came from, used to resolve <c>cref</c>-qualified
    /// <c>&lt;inheritdoc/&gt;</c>; optional (override/interface inheritance still works without it).</param>
    /// <param name="cancellationToken">Honored between pages.</param>
    /// <returns>One page per type, plus the index page when requested.</returns>
    public IReadOnlyList<RenderedPage> Render(
        IReadOnlyList<DocumentedType> surface,
        bool includeIndex,
        Grouping grouping = Grouping.None,
        Compilation? compilation = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surface);

        // The page map holds output-root-relative paths; links are relativized per page below, so a reference
        // resolves correctly regardless of which folder the linking page lives in.
        var pageMap = BuildPageMap(surface, grouping);

        // Index the sources by doc-comment id once, so cref-qualified <inheritdoc> can be resolved per member.
        var sourceMembers = compilation is null ? null : InheritDocResolver.IndexSourceMembers(compilation);

        var pages = new List<RenderedPage>(surface.Count + 1);
        foreach (var type in surface)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = FileNameFor(type.Symbol, grouping);
            var converter = new DocCommentMarkdownConverter(CreateResolver(pageMap, path));
            pages.Add(new RenderedPage
            {
                RelativePath = path,
                Content = RenderType(type, converter, sourceMembers),
            });
        }

        if (includeIndex)
        {
            pages.Add(new RenderedPage { RelativePath = "index.md", Content = RenderIndex(surface, grouping) });
        }

        return pages;
    }

    /// <summary>
    /// Maps each documented type and member doc-comment ID to its page (and member anchor), as an output-root-relative
    /// path. Links are relativized against the linking page at render time.
    /// </summary>
    private static Dictionary<string, string> BuildPageMap(IReadOnlyList<DocumentedType> surface, Grouping grouping)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in surface)
        {
            var file = FileNameFor(type.Symbol, grouping);
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
    /// A cref resolver that links to documented pages, falling back to inline code for anything unmapped. Targets in
    /// <paramref name="pageMap"/> are output-root-relative and relativized against <paramref name="fromPage"/>.
    /// </summary>
    private static CrefResolver CreateResolver(IReadOnlyDictionary<string, string> pageMap, string fromPage)
        => (crefId, displayText) => pageMap.TryGetValue(crefId, out var target)
            ? $"[{displayText ?? DocCommentMarkdownConverter.SimpleNameFromId(crefId)}]({RelativeLink(fromPage, target)})"
            : DocCommentMarkdownConverter.DefaultCrefResolver(crefId, displayText);

    /// <summary>
    /// Rewrites an output-root-relative link target (<c>folder/File.md#anchor</c>) as a path relative to the folder of
    /// the page doing the linking, using forward slashes (the only separator valid in Markdown links). In flat
    /// (<see cref="Grouping.None"/>) layout, where every page sits at the root, this returns the target unchanged.
    /// </summary>
    private static string RelativeLink(string fromPage, string target)
    {
        var hash = target.IndexOf('#');
        var targetPath = hash >= 0 ? target[..hash] : target;
        var anchor = hash >= 0 ? target[hash..] : string.Empty;

        var fromDirectory = Path.GetDirectoryName(fromPage);
        var relative = string.IsNullOrEmpty(fromDirectory)
            ? targetPath
            : Path.GetRelativePath(fromDirectory, targetPath).Replace('\\', '/');

        return relative + anchor;
    }

    /// <summary>
    /// Renders one type's page: header, signature, type docs, then grouped members.
    /// </summary>
    private string RenderType(
        DocumentedType type,
        DocCommentMarkdownConverter converter,
        IReadOnlyDictionary<string, ISymbol>? sourceMembers)
    {
        var sb = new StringBuilder();

        sb.Append("# ").Append(type.Symbol.ToDisplayString(NameWithGenerics)).Append("\n\n");

        if (!type.Symbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.Append("Namespace: `").Append(type.Symbol.ContainingNamespace.ToDisplayString()).Append("`\n\n");
        }

        sb.Append("```csharp\n").Append(TypeSignature(type.Symbol)).Append("\n```\n\n");

        AppendDoc(sb, type.Symbol, converter, sourceMembers);

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
                AppendDoc(sb, member, converter, sourceMembers);
            }
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// Appends a symbol's converted documentation body, if it has any.
    /// </summary>
    private void AppendDoc(
        StringBuilder sb,
        ISymbol symbol,
        DocCommentMarkdownConverter converter,
        IReadOnlyDictionary<string, ISymbol>? sourceMembers)
    {
        var doc = _reader.Read(symbol);
        if (doc is null)
        {
            return;
        }

        // Expand a top-level <inheritdoc/> (cref-named, else the overridden/implemented member) before converting.
        doc = _inheritDoc.Resolve(symbol, doc, sourceMembers);

        var body = converter.Convert(doc);
        if (body.Length > 0)
        {
            sb.Append(body).Append("\n\n");
        }
    }

    /// <summary>
    /// Renders the index page: every documented type, grouped by namespace and linked to its page. The index sits at
    /// the output root, so the page map's root-relative paths are already correct links from here.
    /// </summary>
    private static string RenderIndex(IReadOnlyList<DocumentedType> surface, Grouping grouping)
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
                    .Append(FileNameFor(type.Symbol, grouping))
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
        baseList.AddRange(type.Interfaces
            .Where(@interface => !IsSynthesizedRecordInterface(type, @interface))
            .Select(@interface => @interface.ToDisplayString(TypeReference)));

        return baseList.Count > 0 ? $"{signature} : {string.Join(", ", baseList)}" : signature;
    }

    /// <summary>
    /// True for the <c>IEquatable&lt;TSelf&gt;</c> interface the compiler synthesizes on every record (noise in the
    /// declaration line, never written by the author).
    /// </summary>
    private static bool IsSynthesizedRecordInterface(INamedTypeSymbol type, INamedTypeSymbol @interface)
        => type.IsRecord
            && @interface is { Name: "IEquatable", TypeArguments.Length: 1, ContainingNamespace.Name: "System" }
            && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], type);

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

    /// <summary>
    /// The output-root-relative Markdown path for a type, derived from its doc-comment ID. Flat
    /// (<see cref="Grouping.None"/>): the full type name at the root (eg. <c>Sample.Shapes.Circle.md</c>). By
    /// namespace: a per-namespace folder with the namespace prefix stripped from the file name but the nested-type
    /// chain kept (eg. <c>Sample.Shapes/Circle.md</c>); global-namespace types stay at the root.
    /// </summary>
    private static string FileNameFor(INamedTypeSymbol type, Grouping grouping)
    {
        var id = type.GetDocumentationCommentId() ?? $"T:{type.Name}";
        var fullName = id.StartsWith("T:", StringComparison.Ordinal) ? id[2..] : id;
        // Generic arity markers (`Foo`1`) aren't valid in file names.
        fullName = fullName.Replace('`', '-');

        if (grouping != Grouping.Namespace || type.ContainingNamespace.IsGlobalNamespace)
        {
            return fullName + ".md";
        }

        // The namespace prefix is never affected by the backtick replacement above (it has no generic arity), so its
        // length still indexes into fullName correctly. Strip "Namespace." to leave the (possibly nested) type chain.
        var @namespace = type.ContainingNamespace.ToDisplayString();
        var withinNamespace = fullName[(@namespace.Length + 1)..];
        return $"{@namespace}/{withinNamespace}.md";
    }

    /// <summary>A stable, per-symbol anchor slug (eg. <c>Add(int, int)</c> → <c>add-int-int</c>).</summary>
    private static string AnchorFor(ISymbol member) => Slugify(member.ToDisplayString(MemberTitle));

    private static string Slugify(string text) => NonSlugCharacters().Replace(text.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

}
