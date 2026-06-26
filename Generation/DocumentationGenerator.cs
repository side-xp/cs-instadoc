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
        var pages = new DocumentationRenderer().Render(surface, options.Index, options.Grouping, cancellationToken);
        // Namespace grouping writes into subfolders, so stale-output cleanup must reach them too.
        var recurseCleanup = options.Grouping == Grouping.Namespace;
        var filesWritten = WritePages(pages, options.Output, options.Clean, recurseCleanup, cancellationToken);

        return new GenerationResult
        {
            SourceFilesDiscovered = sourceFiles.Count,
            TypesDocumented = surface.Count,
            FilesWritten = filesWritten,
        };
    }

    /// <summary>
    /// Writes the rendered pages under the output folder, creating directories as needed. When <paramref name="clean"/>
    /// is set, stale Markdown left from a previous run is cleared first (see <see cref="ClearStaleOutput"/>).
    /// </summary>
    /// <returns>The full paths of the files written, in page order.</returns>
    private static IReadOnlyList<string> WritePages(
        IReadOnlyList<RenderedPage> pages,
        string outputFolder,
        bool clean,
        bool recurseCleanup,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputFolder);

        if (clean)
        {
            ClearStaleOutput(outputFolder, recurseCleanup, cancellationToken);
        }

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

    /// <summary>
    /// Deletes Markdown files left in the output folder before a fresh write, so that types renamed or removed since
    /// the last run don't linger as orphans. With <paramref name="recurse"/> set (namespace grouping), per-namespace
    /// subfolders are swept too, and any folder left empty afterwards is removed.
    /// </summary>
    /// <remarks>
    /// Scoped deliberately to <c>*.md</c> files, so non-Markdown content the user keeps alongside the output is left
    /// untouched. In flat layout only the top level is swept, leaving nested folders alone. The extension is re-checked
    /// because the <c>*.md</c> search pattern is matched by the OS rather than by us.
    /// </remarks>
    private static void ClearStaleOutput(string outputFolder, bool recurse, CancellationToken cancellationToken)
    {
        var search = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var stale = Directory.EnumerateFiles(outputFolder, "*.md", search)
            .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var path in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(path);
        }

        if (!recurse)
        {
            return;
        }

        // Remove folders emptied by the sweep (eg. a namespace that no longer has any types), deepest first so a
        // parent left empty by its children's removal is caught in the same pass. The output root itself is kept.
        var directories = Directory.EnumerateDirectories(outputFolder, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length)
            .ToList();

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }

}