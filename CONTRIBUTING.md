# Contributing

First of all, thank you for considering contributing to a *Sideways Experiments* project!

At Sideways Experiments, we hate doing the same thing more than once, and we love to share knowledge. Whether because you "just want to help" or share that generous laziness philosophy, you're welcome!

## Developer setup

### Prerequisites

- **[.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)** or newer (the project targets `net10.0`). Verify your install with `dotnet --version`.
- **Git**: for cloning, and so [MinVer](https://github.com/adamralph/minver) can derive the package version from `vX.Y.Z` tags.
- An editor with C# support: Visual Studio, VS Code with the C# Dev Kit, or JetBrains Rider.

### Get the sources

```sh
git clone https://github.com/side-xp/cs-instadoc.git
cd cs-instadoc
dotnet restore
dotnet build
```

> The solution file is `SideXP.Instadoc.slnx` (the newer XML solution format). Recent `dotnet` CLI and Visual Studio versions open it directly.

## Build and run locally

There are two ways to run the tool: `dotnet run` for quick iteration, and the packaged .NET tool the way an end user installs it.

### During development (`dotnet run`)

Everything after `--` is passed to the tool itself, not to `dotnet run`:

```sh
dotnet run -- --help
dotnet run -- --version
```

A handy smoke test is to point the tool at **this repository's own source** and write the Markdown to a scratch folder:

```sh
dotnet run -- --input ./Generation --input ./Commands --output ./docs/api --index
```

Or scan the whole project in one pass, skipping build output:

```sh
dotnet run -- --input . --exclude "**/bin/**" --exclude "**/obj/**" --output ./docs/api --index
```

### Run the tests

The test suite lives under `tests/SideXP.Instadoc.Tests`. Run it from the repository root:

```sh
dotnet test            # builds and runs every test
dotnet test --nologo   # quieter output
```

All tests must pass before a pull request can be merged (the CI workflow runs them on every push and PR).

### As an installed tool (packaging)

To verify the packaging itself (the `instadoc` command name and tool startup ) pack the project and install it as a global tool from that local folder:

```sh
dotnet pack -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg SideXP.Instadoc

instadoc --input ./Generation --input ./Commands --output ./docs/api --index
```

After a rebuild, update the installed copy, or remove it entirely:

```sh
dotnet tool update --global --add-source ./nupkg SideXP.Instadoc
dotnet tool uninstall --global SideXP.Instadoc
```

> Prefer `dotnet run --` for day-to-day work; only use the pack/install route when you specifically want to test the packaging.

## Get involved!

There's many ways to be involved in our open source projects, and there's absolutely no pressure to give more or less of your time. So whether you want to develop the core of a whole package or just post a comment in an issue, any help is appreciated!

So what can you do to help?

- **Discuss with the community**: join our [Discord server](https://discord.gg/bMK2d47JaE), and start chatting with the community or the core team
- **Report bugs**: found a bug when using one of our packages? You can report it in the *Issues* tab on GitHub
- **Suggest improvements**: whether from the [Discord server](https://discord.gg/bMK2d47JaE) or by creating an *Issue*, feel free to talk about your needs or current usage of our solutions, highlight what's missing or what could be better
- **Request new features**: again, you can use the [Discord server](https://discord.gg/bMK2d47JaE) or create a new *Issue* to ask for something new, see if others may need it too, so we can consider modifying an existing package or even start a new project just for it
- **Address issues**: you can contribute directly to the codebase by resolving an issue, creating the required assets, oe just implement changes and create a Pull request on *GitHub*

> By the way, we love to know how you use our tools, so we can make them better!

## Code of Conduct

*Sideways Experiments* has adopted the [*Contributor Covenant*](https://www.contributor-covenant.org/) as its Code of Conduct for its open source projects, and we expect contributors to adhere to it.

If you observe any unacceptable behavior or need to report a violation, please contact the *Sideways Experiment* core team directly to [contact@sideways-experiments.com](mailto:contact@sideways-experiments.com).

## Code syntax

Please refer to our [general Coding Style specification](https://github.com/side-xp/.github/blob/main/docs/coding-style/README.md) to learn more about the conventions used in this project.

## Submitting a Pull Request

To contribute code:

1. **Fork** this repository
2. Create a **new branch** off `develop`
3. Make your changes and commit them
   - **Signed commits** are required
   - Commits follow [Conventional Commits](https://www.conventionalcommits.org) (`feat:`, `fix:`, `docs:`, `test:`, …); the release tooling reads these to build the changelog and pick the next version
   - Take care to follow our [commit message guidelines](https://github.com/side-xp/.github/blob/main/docs/coding-style/commit-messages.md)
4. Test your changes locally (`dotnet test`)
5. Submit a **pull request targeting `develop`**, not `main`
6. Describe your changes and reference any related issues
7. A member of the ***Sideways Experiments* core team** will review it

As mentioned in the *Suggesting enhancements or features* section, please don't create Pull Requests for unsolicited work.

### Pull Request Requirements

- One PR per logical change (fix, feature, etc.)
- Must pass basic tests (if applicable)
- Must use **signed commits**
- All PRs must be reviewed by a *Sideways Experiments* core team member
- Assign the PR to the core team if not automatically assigned

## Releases

Releases are automated with [Release Please](https://github.com/googleapis/release-please) and run entirely in CI:

1. Conventional commits merged into `main` make Release Please open (or update) a **release pull request** that bumps the changelog and version.
2. Merging that release PR creates the `vX.Y.Z` git tag and a GitHub Release.
3. The publish workflow fires on that release, packs the project, and pushes it to nuget.org.

Each release includes a compiled package and a changelog entry. Contributors do **not** push tags or handle release generation, just write conventional commit messages and the tooling does the rest.

## License and Contributor Agreement

Most of our projects are licensed under the [MIT License](https://mit-license.org).

A `LICENSE` file is always included in our projects' repository root, and all contributions to a specific project will be licensed under the same terms as that file.

We will evaluate whether a Contributor License Agreement (CLA) is required in the future, as the projects go. For now, you can contribute freely under MIT.

## Maintainers

This project is maintained by the team at *Sideways Experiments*.

If you're unsure about any part of the contribution process, feel free to open a discussion on our [Discord server](https://discord.gg/bMK2d47JaE), or by sending an email directly to [contact@sideways-experiments](mailto:contact@sideways-experiments.com).

---

Thank you again for being part of the project!