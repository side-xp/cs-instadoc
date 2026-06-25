using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Utility for parsing <c>.cs</c> files into a Roslyn syntax tree.
/// </summary>
/// <remarks>
/// Parsing is pure and reference-free. It needs neither the BCL nor a build, and it never throws on invalid C#
/// (malformed code simply yields a tree carrying error nodes and diagnostics).
/// Each tree is tagged with its source path and keeps its <c>///</c> documentation comments as structured trivia,
/// ready for the tolerant compilation (see <see cref="DocumentationGenerator"/>).
/// </remarks>
public sealed class SourceParser
{

    /// <summary>
    /// Parse the newest language features, and build the structured XML model behind <c>///</c> comments so the
    /// documentation can be pulled back out later.
    /// </summary>
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Latest, DocumentationMode.Parse);

    /// <summary>
    /// Parses the given files into syntax trees, preserving each file's path and documentation comments.
    /// </summary>
    /// <param name="files">Absolute paths to the <c>.cs</c> files to parse (eg. from <see cref="SourceFileDiscovery"/>).</param>
    /// <param name="cancellationToken">Honored between files.</param>
    /// <returns>One syntax tree per input file, in the same order.</returns>
    public IReadOnlyList<SyntaxTree> Parse(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var trees = new List<SyntaxTree>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read through SourceText so Roslyn detects the file's encoding rather than assuming one.
            using var stream = File.OpenRead(file);
            var text = SourceText.From(stream);

            // path tags the tree so symbols can later be traced back to the file they came from.
            var tree = CSharpSyntaxTree.ParseText(text, ParseOptions, path: file, cancellationToken: cancellationToken);
            trees.Add(tree);
        }

        return trees;
    }

}
