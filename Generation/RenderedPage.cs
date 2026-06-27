namespace SideXP.Instadoc.Generation;

/// <summary>
/// A single Markdown page produced by the renderer, before it is written to disk.
/// </summary>
/// <remarks>
/// Keeping rendering (what to write) separate from writing (the file I/O) lets the whole output be asserted in tests
/// without touching the file system. <see cref="RelativePath"/> is relative to the run's output folder.
/// </remarks>
public sealed record RenderedPage
{

    /// <summary>
    /// Path of the page relative to the output folder (eg. <c>Sample.Shapes.Circle.md</c> or <c>index.md</c>).
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The page's full Markdown content.
    /// </summary>
    public required string Content { get; init; }

}
