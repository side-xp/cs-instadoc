namespace SideXP.Instadoc.Generation;

/// <summary>
/// Generates a documentation as MarkDown files from a C# codebase.
/// </summary>
/// <remarks>
/// This is the internal pipeline of the tool, made independent from the CLI layer.
/// It takes a resolved <see cref="GeneratorOptions"/> and returns a <see cref="GenerationResult"/>, so it can be
/// run from tests without going through Spectre.Console.Cli.
/// </remarks>
public sealed class DocumentationGenerator
{

    /// <summary>
    /// Runs the full documentation generation pipeline for the given options.
    /// </summary>
    /// <param name="options">The resolved, validated inputs for this run.</param>
    /// <param name="cancellationToken">Signaled on Ctrl+C, honored between stages and units of work.</param>
    /// <returns>A summary of what was produced.</returns>
    public GenerationResult Generate(GeneratorOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sourceFiles = new SourceFileDiscovery().Discover(options.Input, options.Exclude);

        cancellationToken.ThrowIfCancellationRequested();

        return new GenerationResult
        {
            SourceFilesDiscovered = sourceFiles.Count,
        };
    }

}