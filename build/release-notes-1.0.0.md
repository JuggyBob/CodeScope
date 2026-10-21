# CodeScope 1.0.0

A native Windows desktop application for [**scc**](https://github.com/boyter/scc),
the fast and accurate code-counting and complexity engine by **Ben Boyter**.

CodeScope analyses a codebase with the pinned, frozen engine and shows the
result as a rich in-app dashboard â€” or as scc's own self-contained HTML
infographic. Everything runs on your machine: no network access, no cloud, no
telemetry, and nothing is ever written to the folder you analyse.

---

## Acknowledging the original creator

**scc was written by [Ben Boyter](https://github.com/boyter) and is released
under the MIT licence.** It is the engine that makes every number in
CodeScope possible, and none of the analysis here is a re-implementation:
CodeScope ships the upstream command-line tool unchanged and drives the very
function `scc --report` calls.

* Upstream project: <https://github.com/boyter/scc>
* Copyright: Copyright (c) 2021 Ben Boyter
* Licence: MIT â€” the full text ships in `vendor/scc-source/LICENSE` and in
  every release

This repository contains a **byte-for-byte snapshot** of scc `4.1.0`
(commit `36b47298e436feec174330a3b7160df61087799f`) under
`vendor/scc-source/`, with a SHA-256 for every file in
`vendor/scc-source/SNAPSHOT-SUMS.sha256`. No upstream file is modified.
scc also vendors its own Go dependencies, each keeping its own licence.

**CodeScope is an independent project. It is not affiliated with, endorsed by,
or supported by the scc project.** Its own code is MIT licensed as well â€” see
[LICENSE](https://github.com/JuggyBob/CodeScope/blob/main/LICENSE) and
[THIRD-PARTY-NOTICES.md](https://github.com/JuggyBob/CodeScope/blob/main/THIRD-PARTY-NOTICES.md).

---

## What it does

* **Pick a folder** with the native Windows folder picker. The app validates it
  and detects whether it lives inside a Git repository.
* **Choose the report contents.** Every optional section the engine supports
  has its own switch, and turning one off omits it from *both* output modes â€”
  the section really is absent, never merely hidden.
* **Fine-tune the analysis.** Ignores, extension filters, large-file limits,
  minified and generated detection, language remapping, ULOC, cognitive
  complexity, COCOMO and LOCOMO parameters, Git window depth and worker
  tuning.
* **Read the results natively.** Metric cards, a language distribution bar, the
  line-length histogram, hotspot and coupling tables, the author rollup with bus
  factor, language and author timelines, the cost models and a searchable
  per-file table.
* **Double-click to reveal.** In the per-file, longest-lines, hotspots and
  coupling tables, a double-click opens the file under the pointer in File
  Explorer. In the coupling table, the column you clicked wins.
* **Sort every table.** Click a column heading to sort by it; the sorted
  heading carries an arrow that points the way the rows run. The arrow and the
  row order are driven by the same state, so they can never disagree.
* **Export HTML.** A themed dialog asks for the file name and the folder.
  Leaving the folder empty writes the report beside the analysed code. An
  existing file is never replaced without an explicit confirmation.
* **Keep the history.** Every finished run is stored with the engine's own
  report data, so a past run can be reopened in the dashboard without running
  the engine again. The last thirty runs are kept.
* **A first-run walkthrough.** Four animated steps that explain the
  application; it can be skipped and never comes back.
* **Presets and themes.** Four built-in presets plus your own, and Dark / Light
  / Follow System, applied live.
* **A Windows installer** that checks for the .NET 8 desktop runtime first and
  can add it when the machine does not have it.

## Screenshots

| | |
| --- | --- |
| ![the report](https://github.com/JuggyBob/CodeScope/blob/main/docs/screenshot-report.png) | ![the configuration](https://github.com/JuggyBob/CodeScope/blob/main/docs/screenshot-config.png) |

---

## What to download

| Asset | Size | What it is |
| --- | --- | --- |
| `CodeScope-Setup.exe` | ~65 MB | **The installer.** Installs for the current user, adds a Start menu shortcut and a proper uninstaller, and offers to add the .NET 8 desktop runtime if the machine does not have it. |
| `CodeScope-portable.zip` | ~11 MB | **No installer.** Unzip it anywhere and run `CodeScope.exe`. Still needs the .NET 8 desktop runtime. |
| `CodeScope-windows-x64-self-contained.zip` | ~70 MB | **Nothing to install first.** Carries the whole .NET runtime, so it runs on a clean Windows machine. Larger because of it. |

## Running it

1. Download `CodeScope-Setup.exe` and run it. If your machine does not have
   the .NET 8 desktop runtime, the installer tells you and offers to add it
   (this needs administrator rights, because a runtime is installed for the
   whole machine). If you would rather not install anything, use the
   self-contained zip.
2. Press **Browseâ€¦**, choose a folder, and press **Analyze Codebase**.
3. Read the report. Double-click a row in any file-bearing table to open that
   file in File Explorer.

The only thing CodeScope asks for is the folder you want to look at. It reads
the code and nothing else.

### The .NET runtime, in short

Windows ships the **.NET Framework 4.8**, which is a different thing and
cannot run this application. The modern **.NET 8** runtime is *not*
preinstalled on any Windows version, so it has to be installed once:

* [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)
  â€” about a minute, one time per machine.
* Check what you already have with `dotnet --list-runtimes`. A line starting
  with `Microsoft.WindowsDesktop.App 8.` means you are done.

Windows 10 version 1607 or later, or Windows 11, 64-bit. .NET 8 does not
support Windows 7 or 8.1.

---

## The two output modes

| | In-app report | HTML export |
| --- | --- | --- |
| Produced by | `codescope-export.exe` â†’ JSON â†’ WPF | `scc.exe --report=<path>` |
| Files written | none, ever | one `.html`, where you choose |
| Section selection | `--report-skip` | `--report-skip` |
| Share card (`og:image`) | n/a â€” it is an HTML concept | included unless switched off |

Both modes run from the same configuration object, so the numbers and the
section selection can never drift apart.

## Privacy

* The application makes **no network request of any kind**. It cannot phone
  home because it has no code that can.
* The **in-app report writes nothing** â€” not to the analysed folder, not
  anywhere else.
* The HTML export writes exactly one `.html` file, to a folder you choose.
* Your settings and the analysis history live in
  `%LOCALAPPDATA%\CodeScope`, never inside an analysed project.

---

## Building it from source

```powershell
git clone https://github.com/JuggyBob/CodeScope.git
cd CodeScope

# the two bundled executables, built from the frozen snapshot
powershell -ExecutionPolicy Bypass -File build\build-backend.ps1

# build the application and run the test suite
powershell -ExecutionPolicy Bypass -File build\build.ps1

# publish
powershell -ExecutionPolicy Bypass -File build\publish.ps1
```

You need the .NET 8 SDK and the Go toolchain the snapshot asks for
(1.26.4). The application itself has **no NuGet packages** â€” every capability
is built on the .NET 8 base class library and WPF â€” so a restore needs no
package feed and the build works offline.

The Windows installer needs [Inno Setup 6](https://jrsoftware.org/isdl.php):

```powershell
winget install --id JRSoftware.InnoSetup --exact --scope user
powershell -ExecutionPolicy Bypass -File build\build-installer.ps1
```

The test suite is a plain console program with no test-framework dependency:

```powershell
dotnet run --project tests\CodeScope.Tests -c Release
```

---

## Contributing

Issues and pull requests are welcome â€” see
[CONTRIBUTING.md](https://github.com/JuggyBob/CodeScope/blob/main/CONTRIBUTING.md)
for the development setup, the branching rules and the pull-request workflow,
and [CODE_OF_CONDUCT.md](https://github.com/JuggyBob/CodeScope/blob/main/CODE_OF_CONDUCT.md)
for the behaviour we expect from everyone taking part.

Security problems should not go in the tracker â€” see
[SECURITY.md](https://github.com/JuggyBob/CodeScope/blob/main/SECURITY.md).

## Licence

CodeScope is released under the [MIT licence](https://github.com/JuggyBob/CodeScope/blob/main/LICENSE).
The bundled engine is a separate MIT-licensed work by Ben Boyter, and its
notice travels with every build. The complete list of third-party licences is
in [THIRD-PARTY-NOTICES.md](https://github.com/JuggyBob/CodeScope/blob/main/THIRD-PARTY-NOTICES.md).
