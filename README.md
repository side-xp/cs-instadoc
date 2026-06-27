# SideXP.Instadoc

`instadoc` generates **API reference documentation as Markdown** from C# *source files*, using [Roslyn](https://github.com/dotnet/roslyn) for source-based analysis.

- **No build required**: it reads your `.cs` files directly, so you don't need to compile the project (or even have its dependencies) to document it.
- **Docs can't drift**: because every page is regenerated from source, the documentation always matches the code it came from.
- **GitHub-friendly output**: one Markdown file per type, with an optional index page and namespace-based folder layout.

## Install

```bash
dotnet tool install --global SideXP.Instadoc
```

## Usage

```bash
instadoc --input ./src --output ./docs/api --index
```

| Option | Purpose |
|---|---|
| `--input` / `-i` | Folder(s) to scan for `.cs` files (repeatable). |
| `--output` / `-o` | Output folder for the generated Markdown. |
| `--visibility` | Comma-separated visibility levels to include; default `public,protected`. |
| `--exclude` | Glob(s) to skip (e.g. `**/Tests/**`, `**/*.Generated.cs`). |
| `--index` | Also write a Markdown index page linking every generated type page. |
| `--no-clean` | Keep existing files in the output folder. By default, stale `.md` files are cleared before writing so renamed/removed types don't linger. |
| `--grouping` | Output layout: `none` (flat, default) or `namespace` (one folder per namespace, named with the full dotted namespace). |
| `--quiet` / `-q` | Suppress informational output for CI use. Errors still go to stderr and the exit code is non-zero on failure. |

## How it works

Instadoc parses every `.cs` file into a Roslyn syntax tree, combines them into a single *tolerant* compilation (the .NET 10 BCL reference assemblies are added, compiler diagnostics are ignored), enumerates the public/protected API surface from symbols, pulls each symbol's XML doc comment, and converts the documentation tags to Markdown (emitting one file per type plus an optional index page).

A tolerant compilation is what lets it run on source alone: missing project references or types it can't resolve degrade gracefully (an unresolved type renders as plain code rather than failing the run).

## Use in CI (GitHub Actions)

Because the docs are derived from source, the natural CI pattern is to regenerate them on every push and commit the result back, so the published Markdown never lags behind the code. Install the tool, run it, and commit only if something changed:

```yaml
name: Docs

on:
  push:
    branches: [main]

jobs:
  docs:
    runs-on: ubuntu-latest
    permissions:
      contents: write          # needed to push the regenerated docs back
    steps:
      - uses: actions/checkout@v7

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.x'

      - name: Install instadoc
        run: dotnet tool install --global SideXP.Instadoc

      - name: Generate API docs
        run: instadoc --input ./src --output ./docs/api --index --quiet

      - name: Commit regenerated docs
        run: |
          git config user.name  "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add docs/api
          git diff --staged --quiet || git commit -m "docs: regenerate API reference"
          git push
```

- **Quiet, but not silent**: `--quiet` keeps the step's log clean; generation failures still surface on stderr and return a non-zero exit code, so a broken run fails the job.
- **No empty commits**: `git diff --staged --quiet || git commit ...` makes the commit a no-op when the output is unchanged, so unrelated pushes don't produce empty docs commits.
- **Review instead of commit**: to gate docs in a pull request rather than committing automatically, drop the commit step and either upload the output as an artifact (`actions/upload-artifact`) or fail the job when `git diff` is non-empty to flag stale docs.

## License

This project is licensed under the [MIT License](https://mit-license.org).

---

Crafted and maintained with love by [Sideways Experiments](https://sideways-experiments.com)

(c) 2022-2026 Sideways Experiments