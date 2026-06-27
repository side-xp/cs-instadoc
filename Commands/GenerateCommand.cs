using System.ComponentModel;
using SideXP.Instadoc.Generation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SideXP.Instadoc.Commands;

/// <summary>
/// The single (default) command: scans C# source folders and generates Markdown API docs.
/// </summary>
public sealed class GenerateCommand : Command<GenerateCommand.Settings>
{

    public sealed class Settings : CommandSettings
    {

        [CommandOption("-i|--input <PATHS>")]
        [Description("Folder(s) to scan for .cs files. Repeatable.")]
        public string[] Input { get; init; } = [];

        [CommandOption("-o|--output <PATH>")]
        [Description("Output folder for the generated Markdown.")]
        public string Output { get; init; } = "docs/api";

        [CommandOption("--visibility <LEVELS>")]
        [Description("Comma-separated visibility levels to include. Default: public,protected.")]
        public string Visibility { get; init; } = "public,protected";

        [CommandOption("--exclude <GLOBS>")]
        [Description("Glob(s) to skip (e.g. **/Tests/**, **/*.Generated.cs). Repeatable.")]
        public string[] Exclude { get; init; } = [];

        [CommandOption("--index")]
        [Description("Also write a Markdown index page linking every generated type page.")]
        public bool Index { get; init; }

        [CommandOption("--no-clean")]
        [Description("Keep existing files in the output folder. By default, stale .md files are cleared before writing.")]
        public bool NoClean { get; init; }

        [CommandOption("-q|--quiet")]
        [Description("Suppress informational output. Errors are still written to stderr and the exit code is non-zero on failure.")]
        public bool Quiet { get; init; }

        [CommandOption("--grouping <MODE>")]
        [Description("Output layout: none (flat, default) or namespace (one folder per namespace).")]
        public string Grouping { get; init; } = "none";

        /// <inheritdoc cref="CommandSettings.Validate"/>
        public override ValidationResult Validate()
        {
            if (Input.Length == 0)
            {
                return ValidationResult.Error("At least one --input folder is required.");
            }

            if (string.IsNullOrWhiteSpace(Output))
            {
                return ValidationResult.Error("--output must not be empty.");
            }

            if (!Enum.TryParse<Generation.Grouping>(Grouping, ignoreCase: true, out var grouping)
                || !Enum.IsDefined(grouping))
            {
                return ValidationResult.Error("--grouping must be one of: none, namespace.");
            }

            return ValidationResult.Success();
        }
    }

    /// <inheritdoc cref="Command{T}.Execute(CommandContext, T, CancellationToken)"/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var options = new GeneratorOptions
        {
            Input = settings.Input,
            Output = settings.Output,
            Visibility = settings.Visibility
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Exclude = settings.Exclude,
            Index = settings.Index,
            Clean = !settings.NoClean,
            Grouping = Enum.Parse<Generation.Grouping>(settings.Grouping, ignoreCase: true),
        };

        if (!settings.Quiet)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Setting");
            table.AddColumn("Value");
            table.AddRow("Input", string.Join("\n", options.Input));
            table.AddRow("Output", options.Output);
            table.AddRow("Visibility", string.Join(", ", options.Visibility));
            table.AddRow("Exclude", options.Exclude.Count == 0 ? "[dim](none)[/]" : string.Join("\n", options.Exclude));
            table.AddRow("Index", options.Index ? "yes" : "no");
            table.AddRow("Clean output", options.Clean ? "yes" : "no");
            table.AddRow("Grouping", options.Grouping.ToString().ToLowerInvariant());
            AnsiConsole.Write(table);
        }

        GenerationResult result;
        try
        {
            result = new DocumentationGenerator().Generate(options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("instadoc: cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"instadoc: {ex.Message}");
            return 1;
        }

        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine(
                $"Generated [green]{result.TypesDocumented}[/] type(s) " +
                $"from [green]{result.SourceFilesDiscovered}[/] source file(s) " +
                $"→ [blue]{options.Output}[/] " +
                $"([green]{result.FilesWritten.Count}[/] file(s) written).");
        }

        return 0;
    }

}