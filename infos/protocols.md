# C# Console App protocols

This document lists the operations to run, implementations to write and tools to use for specific cases that may happen during the development of a C# console app.

> The goal of this document is to be as exhaustive as possible, leaving no doubt when it comes to add/use something in a project. Please report any missing part or need for more details.

## Create the project

Create the project and a solution to hold it with the `dotnet` CLI:

```sh
dotnet new console -n MyApp
dotnet new sln -n MyApp
dotnet sln add MyApp/MyApp.csproj
```

The default `.csproj` baseline used across projects:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

**Note**: Recent Visual Studio creates the newer XML solution format (`.slnx`) instead of the classic `.sln`. Both are accepted by the `dotnet sln` commands; nothing else changes.

## Command-line parsing (Spectre.Console.Cli)

The preferred library for argument parsing, generated help, validation and colored output is [`Spectre.Console.Cli`](https://spectreconsole.net/cli/). Prefer it over hand-rolled `args` parsing for anything beyond a trivial app.

### Add the dependency

```sh
dotnet add package Spectre.Console.Cli
```

### Bootstrap the app

An app with a single action uses the **default command** form, `CommandApp<TCommand>`. In `Program.cs`:

```csharp
using Spectre.Console.Cli;

var app = new CommandApp<DefaultCommand>();

app.Configure(config =>
{
    config.SetApplicationName("myapp");
    config.AddExample("--input", "./some/folder", "--output", "./out");
});

return app.Run(args);
```

**Note**: For multiple sub-commands (e.g. `myapp build`, `myapp clean`), register each with `config.AddCommand<T>("name")` instead of using `CommandApp<T>`.

### Define a command and its options

A command is a `Command<TSettings>` with a nested `Settings` class. Options are plain properties decorated with `[CommandOption]`; the `[Description]` feeds the generated `--help`:

```csharp
using System.ComponentModel;
using Spectre.Console.Cli;

public sealed class DefaultCommand : Command<DefaultCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-i|--input <PATHS>")]
        [Description("Folder(s) to scan. Repeatable.")]
        public string[] Input { get; init; } = [];

        [CommandOption("-o|--output <PATH>")]
        [Description("Output folder.")]
        public string Output { get; init; } = "out";

        [CommandOption("--verbose")]
        [Description("Enable verbose logging.")]
        public bool Verbose { get; init; }
    }

    // ... Execute (see below) ...
}
```

**Note**: An array-typed option (`string[]`) is automatically **repeatable** — `-i ./a -i ./b` collects both. A `bool` option is a **flag**: its presence sets it to `true`, no value needed.

### Validate settings

Override `Validate()` on the settings class to reject bad input *before* `Execute` runs. Return `ValidationResult.Error(...)` to fail with a message, or `ValidationResult.Success()`:

```csharp
public override ValidationResult Validate()
{
    if (Input.Length == 0)
    {
        return ValidationResult.Error("At least one --input folder is required.");
    }
    return ValidationResult.Success();
}
```

### Implement the command body

`Execute` returns the process exit code (`0` = success). **As of Spectre.Console.Cli 0.55, the overload receives a `CancellationToken` and is `protected`:**

```csharp
protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
{
    // ... do the work ...
    return 0;
}
```

**Note**: The `CancellationToken` is signaled on Ctrl+C. Don't check it during instant work — thread it down into long-running stages (file I/O, parsing, network) and check it between units of work / honor it in async I/O, so a cancel aborts cleanly. A `ThrowIfCancellationRequested()` on an instant operation is just ceremony.

## Versioning (MinVer)

The preferred approach is to **derive the version from git tags** with [`MinVer`](https://github.com/adamralph/minver) rather than hardcoding `<Version>` in the `.csproj`. Tagging `v1.2.3` produces version `1.2.3`; between tags you get a prerelease version automatically. A release then needs no manual version bump — you just push a tag.

### Add MinVer

```sh
dotnet add package MinVer
```

**Note**: MinVer marks itself as a development dependency, so `dotnet add package` records it with `PrivateAssets="all"` automatically — it never flows to consumers of your package.

**Note**: When building or packing in CI, check out with full history and tags (`fetch-depth: 0` for `actions/checkout`), otherwise MinVer can't see the tags and falls back to `0.0.0-alpha.0`.

### Surface the version in the CLI

Read the version MinVer stamped onto the assembly and hand it to Spectre so `--version` works:

```csharp
using System.Reflection;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion;
if (!string.IsNullOrWhiteSpace(version))
{
    config.SetApplicationVersion(version);
}
```

## Distribution

### As a .NET tool (NuGet)

A console app can be packaged as a **.NET tool**: a NuGet package that installs an executable command on the user's machine (`dotnet tool install --global <PackageId>` → a command on their `PATH`). This is the preferred way to ship a CLI so it can be installed locally and restored in CI.

The packaging is driven entirely by MSBuild properties — no separate manifest:

```xml
<PropertyGroup>
  <!-- Tool packaging -->
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>myapp</ToolCommandName>
  <PackageId>Vendor.MyApp</PackageId>

  <!-- Metadata shown on nuget.org -->
  <Authors>Your Name</Authors>
  <Description>One-line description shown on nuget.org.</Description>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <RepositoryUrl>https://github.com/you/MyApp</RepositoryUrl>
  <PackageProjectUrl>https://github.com/you/MyApp</PackageProjectUrl>
  <PackageTags>cli;dotnet-tool</PackageTags>
</PropertyGroup>

<ItemGroup>
  <!-- Reused as the NuGet package description page -->
  <None Include="README.md" Pack="true" PackagePath="" />
</ItemGroup>
```

**Note**: `PackageId` must be globally unique on nuget.org; `ToolCommandName` only needs to be unique on each user's machine. They are independent — the package and the command it installs can have different names.

**Note**: `<PackageReadmeFile>` plus the `<None Include="README.md" Pack="true" .../>` item makes the repo `README.md` the package's description page on nuget.org. Write the README for that audience.

### Test the tool locally

During development, run it with `dotnet run`. Everything after `--` is passed to the app itself (not to `dotnet run`):

```sh
dotnet run -- --help
dotnet run -- --version
dotnet run -- -i ./some/folder -o ./out
```

So `dotnet run -- --help` is equivalent to running the installed `myapp --help`.

To verify the **packaging itself** (command name, tool startup) the way an end user experiences it, pack locally and install from that folder as a global tool:

```sh
dotnet pack -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg Vendor.MyApp

myapp --help

# update after a rebuild, or remove:
dotnet tool update --global --add-source ./nupkg Vendor.MyApp
dotnet tool uninstall --global Vendor.MyApp
```

**Note**: Prefer `dotnet run --` for day-to-day iteration; only use the pack/install route when you specifically want to test packaging.

## Publish to NuGet (GitHub Actions)

@todo release workflow triggered by pushing a `v*` git tag: `dotnet pack` then `dotnet nuget push` with a `NUGET_API_KEY` repository secret, checked out with `fetch-depth: 0` so MinVer sees the tags.