namespace SideXP.Instadoc.Generation;

/// <summary>
/// The resolved, validated inputs that drive a documentation run.
/// </summary>
/// <remarks>
/// This is the pipeline's own contract, intentionally decoupled from the CLI layer: the command
/// (see <c>Commands/GenerateCommand.cs</c>) maps its parsed <c>Settings</c> onto this record so the
/// generator never depends on <c>Spectre.Console.Cli</c> and can be exercised from tests directly.
/// </remarks>
public sealed record GeneratorOptions
{

    /// <summary>
    /// One or more folders to recursively scan for <c>.cs</c> files.
    /// </summary>
    public required IReadOnlyList<string> Input { get; init; }

    /// <summary>
    /// Folder that receives the generated Markdown (one file per type, plus the index page when requested).
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Visibility levels to include in the output (eg. <c>public</c>, <c>protected</c>).
    /// </summary>
    public required IReadOnlyList<string> Visibility { get; init; }

    /// <summary>
    /// Globs whose matches are skipped during discovery (eg. <c>**/Tests/**</c>, <c>**/*.Generated.cs</c>).
    /// </summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>
    /// When <see langword="true"/>, also write a Markdown index page linking every generated type page.
    /// </summary>
    public bool Index { get; init; }

}