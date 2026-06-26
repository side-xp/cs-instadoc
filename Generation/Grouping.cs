namespace SideXP.Instadoc.Generation;

/// <summary>
/// How generated type pages are laid out under the output folder.
/// </summary>
/// <remarks>
/// An enum rather than a boolean so future layouts (eg. by assembly, or a recursive namespace tree) can be added
/// without stacking flags.
/// </remarks>
public enum Grouping
{

    /// <summary>
    /// One file per type at the output root, named by full type name (eg. <c>A.B.Foo.md</c>). The default.
    /// </summary>
    None,

    /// <summary>
    /// One folder per namespace, named with the full dotted namespace and <b>non-recursive</b> — <c>A.B</c> and
    /// <c>A.B.C</c> are sibling folders, not nested — with that namespace's type pages inside (named by the type's
    /// name with the namespace prefix stripped but the nested-type chain kept). Global-namespace types stay at the root.
    /// </summary>
    Namespace,

}
