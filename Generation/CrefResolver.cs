namespace SideXP.Instadoc.Generation;

/// <summary>
/// Turns a resolved documentation ID from a <c>cref</c> (eg. <c>T:Namespace.Type</c>, <c>M:Namespace.Type.Method</c>,
/// or <c>!:Name</c> when unresolvable) into the Markdown to emit for it.
/// </summary>
/// <param name="crefId">The documentation comment ID carried by the <c>cref</c> attribute.</param>
/// <param name="displayText">
/// Inner text of the reference when the author supplied one (eg. <c>&lt;see cref="..."&gt;text&lt;/see&gt;</c>),
/// otherwise <see langword="null"/> to let the resolver pick a label.
/// </param>
/// <returns>The Markdown snippet to splice in place of the reference (a link, or inline code as a fallback).</returns>
/// <remarks>
/// The converter (<see cref="DocCommentMarkdownConverter"/>) stays unaware of the page layout: the rendering stage
/// supplies a resolver that maps IDs to <c>Type.md#anchor</c> links, while the default keeps things readable as code.
/// </remarks>
public delegate string CrefResolver(string crefId, string? displayText);
