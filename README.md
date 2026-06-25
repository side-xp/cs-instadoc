# SideXP.Instadoc

`instadoc` generates **API reference documentation as Markdown** from C# *source files*, using [Roslyn](https://github.com/dotnet/roslyn) for source-based analysis.

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
| `--visibility` | Comma list of visibility levels; default `public protected`. |
| `--exclude` | Glob(s) to skip (e.g. `**/Tests/**`, `**/*.Generated.cs`). |
| `--index` | Also write a Markdown index page linking every generated type page. |

## How it works

Instadoc parses every `.cs` file into a Roslyn syntax tree, combines them into a single *tolerant* compilation (the .NET 10 BCL reference assemblies are added, compiler diagnostics are ignored), enumerates the public/protected API surface from symbols, pulls each symbol's XML doc comment, and converts the documentation tags to Markdown — emitting one file per type plus an optional index page.

Because docs are regenerated from source, they can't drift from the code.

## License

This project is licensed under the [MIT License](https://mit-license.org).

---

Crafted and maintained with love by [Sideways Experiments](https://sideways-experiments.com)

(c) 2022-2026 Sideways Experiments