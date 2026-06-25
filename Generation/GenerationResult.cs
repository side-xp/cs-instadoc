namespace SideXP.Instadoc.Generation;

/// <summary>
/// The outcome of a documentation run: what was produced, plus any non-fatal warnings.
/// </summary>
public sealed record GenerationResult
{

    /// <summary>
    /// Number of types selected and rendered.
    /// </summary>
    public int TypesDocumented { get; init; }

    /// <summary>
    /// Absolute or relative paths of the Markdown files written, in write order.
    /// </summary>
    public IReadOnlyList<string> FilesWritten { get; init; } = [];

    /// <summary>
    /// Non-fatal messages collected during the run (eg. unresolved external types, missing docs).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

}