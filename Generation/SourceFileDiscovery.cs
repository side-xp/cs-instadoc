using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Utility for "discovering" source files from given folders.
/// </summary>
/// <remarks>
/// Discovery is source-only and needs no project or solution file: each input folder is recursively globbed for
/// <c>.cs</c> files, minus the caller's exclude patterns. The result feeds the parser (see
/// <see cref="DocumentationGenerator"/>).
/// </remarks>
public sealed class SourceFileDiscovery
{

    /// <summary>
    /// Recursively finds every <c>.cs</c> file under the given input folders, skipping any that match an exclude glob.
    /// </summary>
    /// <param name="inputs">Folders to scan. Relative paths are resolved against the current working directory.</param>
    /// <param name="excludes">
    /// Glob patterns to skip, evaluated relative to each input folder (eg. <c>**/Tests/**</c>, <c>**/*.Generated.cs</c>).
    /// </param>
    /// <returns>
    /// Absolute file paths, de-duplicated (folders may overlap) and ordered for a stable, reproducible run.
    /// </returns>
    public IReadOnlyList<string> Discover(IReadOnlyList<string> inputs, IReadOnlyList<string> excludes)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(excludes);

        // OrdinalIgnoreCase: the same file reached through two overlapping input folders must collapse to one entry,
        // and Windows paths are case-insensitive.
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            var root = Path.GetFullPath(input);
            if (!Directory.Exists(root))
            {
                // A missing folder is silently skipped here; surfacing it as a warning is the caller's concern.
                continue;
            }

            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            matcher.AddInclude("**/*.cs");
            foreach (var exclude in excludes)
            {
                matcher.AddExclude(exclude);
            }

            var match = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
            foreach (var file in match.Files)
            {
                // file.Path is relative to root and uses forward slashes; normalize to an absolute platform path.
                results.Add(Path.GetFullPath(Path.Combine(root, file.Path)));
            }
        }

        var ordered = results.ToList();
        ordered.Sort(StringComparer.OrdinalIgnoreCase);
        return ordered;
    }

}
