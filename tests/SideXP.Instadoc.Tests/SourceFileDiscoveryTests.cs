using SideXP.Instadoc.Generation;

namespace SideXP.Instadoc.Tests;

/// <summary>
/// Tests for <see cref="SourceFileDiscovery"/>, exercised against the sample tree under <c>Fixtures/Sample</c> (copied
/// next to the test binaries at build time).
/// </summary>
public class SourceFileDiscoveryTests
{
    
    /// <summary>
    /// Absolute path to the copied fixture tree. <see cref="AppContext.BaseDirectory"/> is the folder the test
    /// assembly runs from (eg. <c>bin/Debug/net10.0</c>), where the <c>Fixtures</c> content was copied.
    /// </summary>
    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sample");

    [Fact(DisplayName = "Discover all C# files without using --exclude option")]
    public void Discovers_all_cs_files_when_no_excludes()
    {
        var discovery = new SourceFileDiscovery();

        var files = discovery.Discover([SampleRoot], []);

        Assert.Equal(5, files.Count);
        // Every result is an absolute path to an existing .cs file.
        Assert.All(files, path =>
        {
            Assert.True(Path.IsPathFullyQualified(path));
            Assert.EndsWith(".cs", path);
            Assert.True(File.Exists(path));
        });
    }

    [Fact(DisplayName = "Discover all C# files but those in the Tests/ folder")]
    public void Excludes_files_under_a_tests_folder()
    {
        var discovery = new SourceFileDiscovery();

        var files = discovery.Discover([SampleRoot], ["**/Tests/**"]);

        Assert.Equal(4, files.Count);
        Assert.DoesNotContain(files, path => path.Contains("AnimalTests"));
    }

    [Fact(DisplayName = "Discover all C# files but those with *.Generated.cs suffix")]
    public void Excludes_generated_files_by_suffix()
    {
        var discovery = new SourceFileDiscovery();

        var files = discovery.Discover([SampleRoot], ["**/*.Generated.cs"]);

        Assert.Equal(4, files.Count);
        Assert.DoesNotContain(files, path => path.EndsWith("Models.Generated.cs"));
    }

    [Fact(DisplayName = "Discover all C# files but those in the Tests/ folder or with *.Generated.cs suffix")]
    public void Applies_multiple_excludes_together()
    {
        var discovery = new SourceFileDiscovery();

        var files = discovery.Discover([SampleRoot], ["**/Tests/**", "**/*.Generated.cs"]);

        Assert.Equal(3, files.Count);
    }

    [Fact(DisplayName = "Deduplicates files reached through overlapping inputs")]
    public void Deduplicates_files_reached_through_overlapping_inputs()
    {
        var discovery = new SourceFileDiscovery();

        // The same folder passed twice (and once via a child) must not double-count its files.
        var nested = Path.Combine(SampleRoot, "Shapes");
        var files = discovery.Discover([SampleRoot, SampleRoot, nested], []);

        Assert.Equal(5, files.Count);
    }

    [Fact(DisplayName = "Returns results in a stable sorted order")]
    public void Returns_results_in_a_stable_sorted_order()
    {
        var discovery = new SourceFileDiscovery();

        var files = discovery.Discover([SampleRoot], []);

        var sorted = files.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, files);
    }

    [Fact(DisplayName = "Skips input folders that do not exist")]
    public void Skips_input_folders_that_do_not_exist()
    {
        var discovery = new SourceFileDiscovery();

        var missing = Path.Combine(SampleRoot, "does-not-exist");
        var files = discovery.Discover([missing], []);

        Assert.Empty(files);
    }
    
}