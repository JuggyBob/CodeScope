# Contributing to CodeScope

Thanks for considering a contribution. This document covers how to set the
project up, how to branch, and what a pull request is expected to look like.

- [Development environment](#development-environment)
- [Getting the project running](#getting-the-project-running)
- [Branching rules](#branching-rules)
- [Commit messages](#commit-messages)
- [Pull requests](#pull-requests)
- [Tests](#tests)
- [Code style](#code-style)
- [Updating the frozen backend](#updating-the-frozen-backend)
- [Reporting problems](#reporting-problems)

---

## Development environment

| Tool | Version | Needed for |
| --- | --- | --- |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0.4xx or later | Building and running the application |
| [Go toolchain](https://go.dev/dl/) | 1.26.4 (see `vendor/scc-source/go.mod`) | Rebuilding the bundled engine |
| Windows PowerShell | 5.1 or later | The build scripts |
| Git | any recent version | Everything else |

Visual Studio 2022 (17.9 or later, with the ".NET desktop development" workload)
and Rider both work; the project is a plain .NET solution with no package
references.

The application has **no NuGet packages**. JSON, process hosting, theming,
charts and dialogs are all built on the .NET 8 base class library and WPF, so
a restore needs no package feed and the build works offline.

---

## Getting the project running

```powershell
git clone https://github.com/<your-account>/CodeScope.git
cd CodeScope

# Build the two bundled executables from the frozen snapshot in
# vendor/scc-source. This needs the Go toolchain; the result lands in
# backend\ and is a build artefact, so it is not in the repository.
powershell -ExecutionPolicy Bypass -File build\build-backend.ps1

# Build the application and run the test suite.
powershell -ExecutionPolicy Bypass -File build\build.ps1

# Run it.
dotnet run --project src\CodeScope.App
```

If you only want to work on the interface and do not have Go installed:

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1 -SkipBackend
```

The suite then skips the integration tests that drive the real executables and
reports them as skipped rather than silently passing.

---

## Branching rules

* `main` is the stable branch. It is protected: it only moves through a pull
  request, and it only moves when the checks are green.
* Branch from an up-to-date `main` and keep the branch short-lived.
* Name branches after the kind of change:

  | Prefix | Use it for | Example |
  | --- | --- | --- |
  | `feature/` | A new capability | `feature/dark-theme-scheduling` |
  | `fix/` | A defect | `fix/empty-folder-report` |
  | `docs/` | Documentation only | `docs/backend-update-guide` |
  | `chore/` | Tooling, dependencies, build scripts | `chore/single-file-publish` |
  | `release/` | Release preparation | `release/0.2.0` |

* One concern per branch. A pull request that fixes a bug and reformats the
  world will be asked to split.
* Never force-push to `main`. Rebase your own branch instead of merging
  `main` into it, so the history stays readable.

---

## Commit messages

Use the [Conventional Commits](https://www.conventionalcommits.org/) shape:

```
<type>(<scope>): <short summary in the imperative>

<why the change was needed>

<what changed, and anything a reviewer needs to know>
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`,
`ci`, `chore`. A `BREAKING CHANGE:` line in the body marks a breaking change,
which requires a major version bump.

Examples:

```
fix(report): keep the sort arrow and the row order in step

The arrow identified a column by its heading alone, so a heading that
appears in two tables could never show its arrow, and the four tables
sorted by the grid could disagree with the arrow's direction.
```

---

## Pull requests

1. **Open an issue first** for anything larger than a small fix, so the
   approach can be agreed before the work is done.
2. **Keep the change reviewable.** One pull request should be one idea. Split
   unrelated work.
3. **Describe the why.** The diff already shows the what. Say what was wrong,
   what you changed, and how you know it works.
4. **Add or update tests.** A behavioural change without a test will be asked
   for one. The suite is a console program — see [Tests](#tests).
5. **Update the documentation** that the change affects, including
   `CHANGELOG.md` under the *Unreleased* heading.
6. **Update the screenshots** in `docs/` if the interface changed visibly.
7. **Do not include build output.** `bin/`, `obj/`, `publish/` and `backend/`
   are ignored by `.gitignore`; the engine is rebuilt from the snapshot.
8. **Let the checks run.** Continuous integration builds the solution, runs the
   whole suite and publishes the application. A red check is a conversation,
   not an obstacle to work around.

Maintainers aim to review within a week. If a pull request has been quiet for
longer than that, say so in a comment — it is almost always a nudge we
needed.

---

## Tests

```powershell
# everything: build the engine, build the app, run the suite
powershell -ExecutionPolicy Bypass -File build\build.ps1

# the suite on its own
dotnet run --project tests\CodeScope.Tests -c Release

# the suite without the integration tests that need the engine
powershell -ExecutionPolicy Bypass -File build\build.ps1 -SkipBackend
```

The suite has no test-framework dependency — it is a console program that
prints one line per check and returns a non-zero exit code when something
fails, which is all the CI needs.

Two kinds of test live in the project:

* **Unit** — the argument builder, the section catalogue, the settings and
  preset stores, the history store, the report view model, the formatters.
* **Integration** — the real bundled executables against throwaway fixtures:
  the engine's flags, a real Git repository, section selection in both output
  modes, the overwrite confirmation, and the guarantee that the in-app mode
  writes nothing to disk.

When you touch the engine integration, add an integration test. When you touch
anything else, a unit test is usually enough.

---

## Code style

`.editorconfig` carries the rules, and every mainstream editor reads it. The
short version:

* Four spaces, braces on their own line, `var` where the type is obvious.
* `file`-scoped namespaces.
* `dotnet format` the solution before you commit; the CI does not reformat
  for you.
* XAML: two spaces of indentation, every colour through a `DynamicResource`
  theme brush, every corner radius through the `Radius*` tokens.
* Comments explain *why*, not *what*. A comment that repeats the line above it
  is noise.
* Public types and non-obvious methods get a short summary; the rest do not
  need one.

Keep the interface in one language — English — and keep the wording of the
interface consistent with the existing text.

---

## Updating the frozen backend

The engine is pinned to an exact upstream commit and must stay that way. The
procedure, including how to verify the snapshot afterwards, is written down in
`BACKEND-UPDATE.md`. A backend bump is its own pull request, with the new
commit and version in the table at the top of `README.md`.

---

## Reporting problems

Bugs and feature requests belong in the issue tracker — the templates ask for
the version, the folder you analysed and what you expected to happen.

**Security problems do not go in the tracker.** Follow
[SECURITY.md](SECURITY.md) and report them privately.

Everyone taking part is expected to follow the
[Code of Conduct](CODE_OF_CONDUCT.md).
