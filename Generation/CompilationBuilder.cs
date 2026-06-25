using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SideXP.Instadoc.Generation;

/// <summary>
/// Utility that combines a parsed syntax trees into a single, deliberately tolerant Roslyn compilation.
/// </summary>
/// <remarks>
/// The trees are compiled against the .NET Base Class Library reference assemblies, but the resulting compiler
/// diagnostics are <em>ignored</em>: the compilation is never emitted, only queried for symbols. This is the trick
/// that lets the tool document code without a build: types declared in the sources and in the BCL resolve to real
/// symbols (so signatures, inherited members and cross-references are accurate), while any type that can't be resolved
/// (a framework or third-party type with no reference here) comes back as an <see cref="IErrorTypeSymbol"/> that later
/// stages render as plain text rather than a broken link.
/// </remarks>
public sealed class CompilationBuilder
{

    /// <summary>
    /// Name given to the in-memory assembly. It never reaches disk (the compilation is not emitted), so it is purely a
    /// label for diagnostics and symbol identity.
    /// </summary>
    private const string AssemblyName = "SideXP.Instadoc.Generated";

    /// <summary>
    /// Builds a tolerant compilation from the given syntax trees, with the .NET BCL reference assemblies attached.
    /// </summary>
    /// <param name="syntaxTrees">The parsed sources (eg. from <see cref="SourceParser"/>).</param>
    /// <param name="cancellationToken">Checked before the (single-shot) compilation is created.</param>
    /// <returns>A compilation ready to be queried for symbols. Compiler errors are intentionally not surfaced.</returns>
    public CSharpCompilation Build(IReadOnlyList<SyntaxTree> syntaxTrees, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syntaxTrees);
        cancellationToken.ThrowIfCancellationRequested();

        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        return CSharpCompilation.Create(
            AssemblyName,
            syntaxTrees,
            Net100.References.All,
            options);
    }

}
