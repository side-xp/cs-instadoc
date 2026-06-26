using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// End-to-end tests for <see cref="DocumentationGenerator"/>, focused on how it manages the output folder (the
/// write step is otherwise covered indirectly by the renderer tests). Each test runs the full pipeline against the
/// <c>Fixtures/Sample</c> tree into a throwaway temp folder.
/// </summary>
public class DocumentationGeneratorTests : IDisposable
{

    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sample");

    /// <summary>A fresh, empty output folder unique to this test instance, removed on <see cref="Dispose"/>.</summary>
    private readonly string _output = Path.Combine(Path.GetTempPath(), "instadoc-tests", Guid.NewGuid().ToString("N"));

    private GeneratorOptions OptionsWith(bool clean, Grouping grouping = Grouping.None) => new()
    {
        Input = [SampleRoot],
        Output = _output,
        Visibility = ["public", "protected"],
        Index = true,
        Clean = clean,
        Grouping = grouping,
    };

    [Fact(DisplayName = "Generation clears stale Markdown from the output folder by default")]
    public void Clears_stale_markdown_by_default()
    {
        Directory.CreateDirectory(_output);
        var orphan = Path.Combine(_output, "RenamedAwayType.md");
        var foreign = Path.Combine(_output, "notes.txt");
        File.WriteAllText(orphan, "stale");
        File.WriteAllText(foreign, "keep me");

        var result = new DocumentationGenerator().Generate(OptionsWith(clean: true));

        Assert.False(File.Exists(orphan), "the orphan .md should have been cleared");
        Assert.True(File.Exists(foreign), "non-Markdown files must be left untouched");
        Assert.True(File.Exists(Path.Combine(_output, "index.md")));
        Assert.NotEmpty(result.FilesWritten);
    }

    [Fact(DisplayName = "Generation keeps existing files when cleaning is disabled")]
    public void Keeps_existing_files_when_clean_is_disabled()
    {
        Directory.CreateDirectory(_output);
        var orphan = Path.Combine(_output, "RenamedAwayType.md");
        File.WriteAllText(orphan, "stale");

        new DocumentationGenerator().Generate(OptionsWith(clean: false));

        Assert.True(File.Exists(orphan), "with cleaning off, pre-existing .md files should survive");
        Assert.True(File.Exists(Path.Combine(_output, "index.md")));
    }

    [Fact(DisplayName = "Namespace grouping writes per-namespace folders to disk")]
    public void Namespace_grouping_writes_folders()
    {
        new DocumentationGenerator().Generate(OptionsWith(clean: true, Grouping.Namespace));

        Assert.True(File.Exists(Path.Combine(_output, "Sample.Shapes", "Circle.md")));
        Assert.True(File.Exists(Path.Combine(_output, "index.md")));
    }

    [Fact(DisplayName = "Cleanup under namespace grouping sweeps subfolders and prunes emptied ones")]
    public void Namespace_grouping_cleanup_is_recursive()
    {
        Directory.CreateDirectory(_output);
        // A whole namespace folder that no longer exists in the sources.
        var staleFolder = Path.Combine(_output, "Gone.Namespace");
        Directory.CreateDirectory(staleFolder);
        var orphan = Path.Combine(staleFolder, "RemovedType.md");
        File.WriteAllText(orphan, "stale");

        new DocumentationGenerator().Generate(OptionsWith(clean: true, Grouping.Namespace));

        Assert.False(File.Exists(orphan), "an orphan .md in a subfolder should have been cleared");
        Assert.False(Directory.Exists(staleFolder), "a folder emptied by the sweep should be pruned");
        Assert.True(File.Exists(Path.Combine(_output, "Sample.Shapes", "Circle.md")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_output))
        {
            Directory.Delete(_output, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

}
