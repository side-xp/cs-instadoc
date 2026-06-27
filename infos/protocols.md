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

## File globbing (Microsoft.Extensions.FileSystemGlobbing)

When the app needs to find files by pattern — recursively, with include **and** exclude rules — use [`Microsoft.Extensions.FileSystemGlobbing`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.filesystemglobbing) rather than hand-rolling `Directory.EnumerateFiles` plus manual filtering. It understands `**` recursive wildcards and layered include/exclude patterns, which is exactly the `--input` / `--exclude` shape most CLIs expose.

### Add the dependency

```sh
dotnet add package Microsoft.Extensions.FileSystemGlobbing
```

### Match files under a folder

Build a `Matcher`, register include and exclude globs, then run it against a directory. `Execute` takes a `DirectoryInfoWrapper`, not a raw path string:

```csharp
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
matcher.AddInclude("**/*.cs");
matcher.AddExclude("**/Tests/**");
matcher.AddExclude("**/*.Generated.cs");

var root = Path.GetFullPath("./src");
var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));

foreach (var file in result.Files)
{
    // file.Path is relative to `root` and uses forward slashes.
    var absolute = Path.GetFullPath(Path.Combine(root, file.Path));
    Console.WriteLine(absolute);
}
```

**Note**: Patterns are evaluated **relative to the matched directory**, so `**/Tests/**` and `**/*.Generated.cs` work without anchoring them to an absolute path. The `**` token is what makes a pattern recursive — `*.cs` on its own matches only the top level.

**Note**: `result.Files` yields **relative** paths with forward slashes (`a/b/File.cs`) on every OS. Re-anchor each one with `Path.Combine(root, file.Path)` then `Path.GetFullPath(...)` to get a normalized, absolute path with the platform's separators.

**Note**: The `Matcher` constructor takes a `StringComparison`. Use `OrdinalIgnoreCase` on Windows (it also collapses the same file reached through two roots into one match); pass `Ordinal` only if you specifically need case-sensitive matching.

### Scan multiple folders

A `Matcher` runs against one root at a time, but the same instance is reusable — the registered patterns stay put, only the directory passed to `Execute` changes. For several `--input` folders, run it per folder and merge into a set so overlapping roots don't produce duplicates:

```csharp
var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var input in inputs)
{
    var root = Path.GetFullPath(input);
    if (!Directory.Exists(root))
    {
        continue; // skip missing folders (or surface a warning to the caller)
    }

    var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
    foreach (var file in result.Files)
    {
        files.Add(Path.GetFullPath(Path.Combine(root, file.Path)));
    }
}
```

**Note**: Sort the final set before use (`StringComparer.OrdinalIgnoreCase`) if the output needs to be deterministic across runs — the file system enumeration order is not guaranteed.

## Testing (xUnit)

The preferred test framework is [xUnit](https://xunit.net/). Tests live in their **own project** (one per project under test), never mixed into the application or library being tested — a test project references the project under test, exercises its public surface, and is itself never shipped.

### Create the test project

By convention, test projects live under a `tests/` folder and are named `<ProjectName>.Tests`:

```sh
dotnet new xunit -o tests/MyApp.Tests -n MyApp.Tests
```

The template already targets the current framework, sets `<IsPackable>false</IsPackable>` (a test project is never packed or published), and pulls in the three packages that make it work:

- **`xunit`** — the `[Fact]` attribute and the `Assert` API.
- **`Microsoft.NET.Test.Sdk`** — the MSBuild/runtime glue that makes the project runnable as a test suite.
- **`xunit.runner.visualstudio`** — lets `dotnet test` and the Visual Studio Test Explorer discover and run the tests.

### Wire it into the solution

Add the new project to the solution, and reference the project under test so the tests can `using` its types:

```sh
dotnet sln add tests/MyApp.Tests/MyApp.Tests.csproj
dotnet add tests/MyApp.Tests/MyApp.Tests.csproj reference MyApp.csproj
```

**Note**: `dotnet sln` works with both the classic `.sln` and the newer `.slnx` solution formats — pass whichever the repo uses (`dotnet sln MyApp.slnx add ...`), or omit the name and the CLI finds the single solution in the folder.

**Note**: A test project references the project under test, **never the reverse**. The reference gives the tests access to every `public` type; nothing about testing should leak into the shipped project.

### Write a test

A test is a `public` method marked `[Fact]`, grouped into a plain class. The runner discovers and runs every `[Fact]` in isolation. The standard shape is **Arrange → Act → Assert**:

```csharp
using MyApp;

namespace MyApp.Tests;

public class CalculatorTests
{
    [Fact]
    public void Adds_two_numbers()
    {
        var calculator = new Calculator();   // Arrange
        var result = calculator.Add(2, 3);    // Act
        Assert.Equal(5, result);              // Assert
    }
}
```

A test fails if any `Assert` fails or the method throws; otherwise it passes. Common assertions: `Assert.Equal`, `Assert.True`/`False`, `Assert.Contains`/`DoesNotContain`, `Assert.Throws<T>`, `Assert.Empty`.

**Note**: For the same test run against many inputs, use `[Theory]` with `[InlineData(...)]` rows instead of copy-pasting `[Fact]` methods — each row becomes its own reported test case.

### Run the tests

```sh
dotnet test                  # builds and runs every test project in the solution
dotnet test --nologo         # quieter output
```

In Visual Studio, *Test → Test Explorer* lists every `[Fact]` with pass/fail status and can run or **debug** a single test. Both routes use the same runner packages.

### Tests that need files on disk (fixtures)

When tests need real sample files (inputs to parse, scan, or read), keep them as **fixtures** that are *not* compiled into the test assembly but *are* copied next to the test binaries so they exist at runtime. In the test `.csproj`:

```xml
<ItemGroup>
  <Compile Remove="Fixtures/**/*.cs" />
  <Content Include="Fixtures/**/*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Locate them at runtime relative to the test assembly, via `AppContext.BaseDirectory` (the folder the test `.dll` runs from, eg. `bin/Debug/net10.0`):

```csharp
var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
```

**Note**: The `<Compile Remove>` matters whenever fixtures are `.cs` files — otherwise the SDK's default `**/*.cs` glob would compile them as part of the test assembly. Fixtures are *data*, not code.

### Gotcha: a project at the repository root

If the project under test sits at the **repository root** (rather than in its own `src/` subfolder), its default `**/*.cs` glob extends over the whole tree — including `tests/` — and the project will try to compile the test sources, failing because they reference test-only packages. Exclude the test tree from the main project's `.csproj`:

```xml
<ItemGroup>
  <Compile Remove="tests/**/*.cs" />
</ItemGroup>
```

**Note**: This is a symptom of any root-level project, not of testing specifically — the default compile glob sweeps every `.cs` below the project file. A project laid out under `src/` with tests under `tests/` sidesteps it entirely.

## Versioning (MinVer)

The preferred approach is to **derive the version from git tags** with [`MinVer`](https://github.com/adamralph/minver) rather than hardcoding `<Version>` in the `.csproj`. Tagging a release produces a matching version; between tags you get a prerelease version automatically. A release then needs no manual version bump — you just push a tag.

### Add MinVer

```sh
dotnet add package MinVer
```

**Note**: MinVer marks itself as a development dependency, so `dotnet add package` records it with `PrivateAssets="all"` automatically — it never flows to consumers of your package.

### Match the tag prefix

By default MinVer only recognizes tags with **no prefix** (`1.2.3`). The common convention — and what Release Please produces — is a `v` prefix (`v1.2.3`). If your tags carry a prefix you **must** tell MinVer about it, otherwise it ignores every tag, finds none, and silently falls back to `0.0.0-alpha.0.{height}`:

```xml
<PropertyGroup>
  <!-- Tags are `v1.2.3`; MinVer defaults to no prefix, so it must be told about the `v`. -->
  <MinVerTagPrefix>v</MinVerTagPrefix>
</PropertyGroup>
```

**Note**: This is the single most common reason a MinVer package publishes as `0.0.0-alpha.0.x` despite real tags existing — the tags are there, just not in the shape MinVer was looking for. The fallback is identical to the "no tags reachable" case, which makes it easy to misdiagnose as a checkout/fetch problem.

**Note**: When building or packing in CI, also check out with full history and tags (`fetch-depth: 0` for `actions/checkout`), otherwise MinVer genuinely can't see the tags and falls back to `0.0.0-alpha.0` for that reason instead.

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

The release process uses [Release Please](https://github.com/googleapis/release-please) for version management and a dedicated GitHub Actions workflow for publishing. Release Please reads conventional commits, maintains `CHANGELOG.md`, and creates a tagged GitHub Release when a release PR is merged. The publish workflow fires on that release and pushes the package to nuget.org using [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) — keyless OIDC-based authentication, no long-lived secrets.

### Additional package metadata

The properties covered in [As a .NET tool (NuGet)](#as-a-net-tool-nuget) are the minimum. Before a first publish, also add:

```xml
<PropertyGroup>
  <Title>My Tool</Title>
  <Copyright>Copyright (c) 2024 Author Name</Copyright>
  <PackageIcon>icon.png</PackageIcon>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>

<ItemGroup>
  <None Include="icon.png" Pack="true" PackagePath="" />
</ItemGroup>
```

- **`Title`** — human-friendly display name shown in the nuget.org UI. Without it, nuget.org falls back to `PackageId`.
- **`Copyright`** — shown on the package page.
- **`PackageIcon`** — a PNG file (128×128 px minimum) shown in search results. Without one you get a generic grey placeholder.
- **`RepositoryType`** — enables Source Link integrations.

**Note**: `README.md` is already declared as `PackageReadmeFile` and included as a `<None>` item, so it becomes the description page on nuget.org as-is. Write it for that audience before first publish.

### Enable Trusted Publishing on nuget.org (one-time)

Trusted Publishing issues short-lived OIDC tokens from GitHub Actions instead of storing a long-lived API key. Nothing to rotate, nothing to leak.

1. Log in to [nuget.org](https://www.nuget.org) → click your username → **Trusted Publishing**
2. Add a new policy with these values (case-insensitive):
   - **Repository Owner:** the GitHub organization or user name (e.g. `my-org`)
   - **Repository:** the repository name (e.g. `my-repo`)
   - **Workflow file:** `release.yml` (filename only, no path)
   - **Environment:** leave empty
3. Set the policy owner to your nuget.org account or organization

The policy becomes permanently active after the first successful publish. Until then it is temporarily active for 7 days — if no publish happens in that window, it deactivates, but can be reactivated at any time.

### Configure the GitHub repository (one-time)

1. **Settings → Actions → General → Workflow permissions:** enable *Allow GitHub Actions to create and approve pull requests* — Release Please needs this to open release PRs.
2. **Settings → Variables → Actions → New repository variable:** add `NUGET_USER` set to your nuget.org profile name (not your email — find it at the top-right of nuget.org after logging in).

### Add the workflow files

Two workflow files are needed under `.github/workflows/`.

#### CI (`ci.yml`)

Runs tests on every push and pull request. `paths-ignore` skips the job when the only changes are Release Please bookkeeping files (`CHANGELOG.md`, `version.txt`, `.release-please-manifest.json`), avoiding a redundant test run every time a release PR is merged.

```yml
name: CI

on:
  push:
    branches: [main, develop]
    paths-ignore:
      - 'CHANGELOG.md'
      - 'version.txt'
      - '.release-please-manifest.json'
  pull_request:
    branches: [main, develop]
    paths-ignore:
      - 'CHANGELOG.md'
      - 'version.txt'
      - '.release-please-manifest.json'

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release
```

#### Release Please + publish (`release.yml`)

Fires on every push to `main`. The `release-please` job reads conventional commits since the last release and creates or updates a release PR that bumps `CHANGELOG.md` and `version.txt`. When that PR is merged, `release-please` creates the git tag and GitHub Release, then the `publish` job runs conditionally based on the `release_created` output.

The publish job is colocated here rather than in a separate workflow because GitHub Actions does not fire events triggered by `GITHUB_TOKEN` (the token Release Please uses to create the release) — a separate workflow listening on `release: [published]` would never trigger.

```yml
name: Release Please

on:
  push:
    branches: [main]

permissions:
  contents: write
  pull-requests: write

jobs:
  release-please:
    runs-on: ubuntu-latest
    outputs:
      release_created: ${{ steps.release.outputs.release_created }}
      tag_name: ${{ steps.release.outputs.tag_name }}
    steps:
      - uses: googleapis/release-please-action@v5
        id: release
        with:
          config-file: release-please-config.json
          manifest-file: .release-please-manifest.json

  publish:
    needs: release-please
    if: needs.release-please.outputs.release_created == 'true'
    runs-on: ubuntu-latest
    permissions:
      id-token: write  # required for OIDC token issuance (Trusted Publishing)
      contents: read
    steps:
      - uses: actions/checkout@v7
        with:
          ref: ${{ needs.release-please.outputs.tag_name }}  # check out the tag directly so MinVer sees it at HEAD
          fetch-depth: 0
          fetch-tags: true

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release

      - name: Pack
        run: dotnet pack MyApp.csproj --no-build --configuration Release --output ./nupkg

      - name: Login to NuGet (Trusted Publishing)
        uses: NuGet/login@v1
        id: nuget-login
        with:
          user: ${{ vars.NUGET_USER }}

      - name: Push to NuGet
        run: >
          dotnet nuget push ./nupkg/*.nupkg
          --api-key ${{ steps.nuget-login.outputs.NUGET_API_KEY }}
          --source https://api.nuget.org/v3/index.json
          --skip-duplicate
```

Two config files are required at the repository root alongside the workflows.

`release-please-config.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json",
  "packages": {
    ".": {
      "release-type": "simple",
      "changelog-path": "CHANGELOG.md",
      "bump-minor-pre-major": true,
      "bump-patch-for-minor-pre-major": true
    }
  }
}
```

`.release-please-manifest.json` — tracks the current released version; Release Please updates this file in every release PR:

```json
{
  ".": "0.0.0"
}
```

**Note**: `release-type: simple` manages a `version.txt` bookkeeping file alongside `CHANGELOG.md`. The actual package version still comes from MinVer reading the git tag — the two don't interfere.

**Note**: Release Please tags releases as `v1.2.3` (with the `v` prefix). For MinVer to read that tag the project **must** set `<MinVerTagPrefix>v</MinVerTagPrefix>` — see [Match the tag prefix](#match-the-tag-prefix). Without it, every published package is stamped `0.0.0-alpha.0.x` even though tagging, the release, and the push all succeed.

**Note**: `bump-minor-pre-major` and `bump-patch-for-minor-pre-major` prevent `feat:` commits from bumping the major version while the package is pre-1.0. Remove both options once you're ready to publish a stable version.

**Note**: `id-token: write` on the `publish` job allows GitHub Actions to issue the OIDC token that NuGet's Trusted Publishing validates. Without it, the `NuGet/login@v1` step fails. The temporary API key is valid for 1 hour.

**Note**: `dotnet pack` targets the project file directly (not the solution) to avoid attempting to pack the test project.