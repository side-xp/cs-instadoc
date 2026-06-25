using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SideXP.Instadoc.Commands;

/// <summary>
/// The single (default) command: scans C# source folders and generates Markdown API docs.
/// </summary>
/// <remarks>
/// The generation pipeline itself is not implemented yet — this command currently validates
/// and echoes the resolved settings. See <c>infos/instadoc-project-guide.md</c> (§2) for the
/// seven-step pipeline this will drive.
/// </remarks>
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

        [CommandOption("--nav")]
        [Description("Also write the MkDocs nav/index for the generated pages.")]
        public bool Nav { get; init; }

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

            return ValidationResult.Success();
        }
    }

    /// <inheritdoc cref="Command{T}.Execute(CommandContext, T, CancellationToken)"/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var visibility = settings.Visibility
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        AnsiConsole.MarkupLine("[bold]instadoc[/] — resolved settings:");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Input", string.Join("\n", settings.Input));
        table.AddRow("Output", settings.Output);
        table.AddRow("Visibility", string.Join(", ", visibility));
        table.AddRow("Exclude", settings.Exclude.Length == 0 ? "[dim](none)[/]" : string.Join("\n", settings.Exclude));
        table.AddRow("Nav", settings.Nav ? "yes" : "no");
        AnsiConsole.Write(table);

        // TODO: drive the generation pipeline (discover -> parse -> compile -> enumerate ->
        // pull docs -> convert to Markdown -> render & write). See guide §2. Thread
        // `cancellationToken` through each stage so a Ctrl+C aborts cleanly between files/types
        // and cancels any async I/O.
        AnsiConsole.MarkupLine("[yellow]The generation pipeline is not implemented yet.[/]");

        return 0;
    }

}