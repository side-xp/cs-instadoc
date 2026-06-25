using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <see cref="SourceParser"/>, exercised against the sample tree under <c>Fixtures</c>
/// (copied next to the test binaries at build time).
/// </summary>
public class SourceParserTests
{

    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sample");

    private static string AnimalFile => Path.Combine(SampleRoot, "Animal.cs");

    private static string BrokenFile => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Invalid", "Broken.cs");

    [Fact(DisplayName = "Parses one tree per file in order")]
    public void Parses_one_tree_per_file_in_order()
    {
        var files = new SourceFileDiscovery().Discover([SampleRoot], []);

        var trees = new SourceParser().Parse(files);

        Assert.Equal(files.Count, trees.Count);
        // The tree at each position came from the file at the same position.
        Assert.Equal(files, trees.Select(tree => tree.FilePath).ToList());
    }

    [Fact(DisplayName = "Tags each tree with its source path")]
    public void Tags_each_tree_with_its_source_path()
    {
        var trees = new SourceParser().Parse([AnimalFile]);

        Assert.Equal(AnimalFile, Assert.Single(trees).FilePath);
    }

    [Fact(DisplayName = "Retains documentation comments as structured trivia")]
    public void Retains_documentation_comments_as_structured_trivia()
    {
        var tree = Assert.Single(new SourceParser().Parse([AnimalFile]));

        var animal = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(node => node.Identifier.Text == "Animal");

        // The structured DocumentationCommentTrivia only exists because the parser uses DocumentationMode.Parse.
        var hasStructuredDoc = animal.GetLeadingTrivia()
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .Any();

        Assert.True(hasStructuredDoc);
    }

    [Fact(DisplayName = "Parses invalid source without throwing")]
    public void Parses_invalid_source_without_throwing()
    {
        var tree = Assert.Single(new SourceParser().Parse([BrokenFile]));

        // Tolerance is the whole point: a tree is produced, and the errors surface as diagnostics rather than exceptions.
        Assert.Contains(tree.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

}
