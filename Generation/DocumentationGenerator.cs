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

        // Step 1: Discover the .cs files to document.
        var sourceFiles = new SourceFileDiscovery().Discover(options.Input, options.Exclude);
        // Step 2: Parse each file into a Roslyn syntax tree.
        var syntaxTrees = new SourceParser().Parse(sourceFiles, cancellationToken);
        // Step 3: Build a tolerant compilation (own + BCL types resolve, unknown externals degrade to error symbols).
        var compilation = new CompilationBuilder().Build(syntaxTrees, cancellationToken);
        // Step 4: Enumerate the API surface (types + members matching the requested visibility).
        var surface = new ApiSurfaceExtractor().Extract(compilation, options.Visibility, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // @todo Steps 5-7: pull each symbol's doc comment, convert to Markdown, and write one file per type.

        return new GenerationResult
        {
            SourceFilesDiscovered = sourceFiles.Count,
            TypesDocumented = surface.Count,
        };
    }

}