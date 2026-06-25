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

        // Discover the .cs files to document.
        var sourceFiles = new SourceFileDiscovery().Discover(options.Input, options.Exclude);
        // Parse each file into a Roslyn syntax tree.
        var syntaxTrees = new SourceParser().Parse(sourceFiles, cancellationToken);
        // Build a tolerant compilation (own + BCL types resolve, unknown externals degrade to error symbols).
        var compilation = new CompilationBuilder().Build(syntaxTrees, cancellationToken);
        // Enumerate the API surface (types + members matching the requested visibility).
        var surface = new ApiSurfaceExtractor().Extract(compilation, options.Visibility, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Render one Markdown page per type (pulling and converting doc comments), then write them out.
        var pages = new DocumentationRenderer().Render(surface, options.Index, cancellationToken);
        var filesWritten = WritePages(pages, options.Output, cancellationToken);

        return new GenerationResult
        {
            SourceFilesDiscovered = sourceFiles.Count,
            TypesDocumented = surface.Count,
            FilesWritten = filesWritten,
        };
    }

    /// <summary>
    /// Writes the rendered pages under the output folder, creating directories as needed.
    /// </summary>
    /// <returns>The full paths of the files written, in page order.</returns>
    private static IReadOnlyList<string> WritePages(
        IReadOnlyList<RenderedPage> pages,
        string outputFolder,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputFolder);

        var written = new List<string>(pages.Count);
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = Path.Combine(outputFolder, page.RelativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, page.Content);
            written.Add(path);
        }

        return written;
    }

}