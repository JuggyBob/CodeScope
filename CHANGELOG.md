# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Version numbers are for **CodeScope itself**. The bundled scc engine has its own
version, which is pinned to an exact commit and listed in the
[README](README.md#the-frozen-backend); changing it is a separate, explicit
step.

---

## [Unreleased]

Nothing yet.

## [0.1.0] — 2026-10-21

The first public release.

### Added

- **A Windows installer.** `CodeScope-Setup.exe` installs the application for
  the current user, adds a Start menu shortcut and a proper uninstaller, and
  checks for the .NET 8 desktop runtime first: when it is missing, the setup
  carries a copy of Microsoft's own runtime installer and offers to add it, so
  the application works the moment it is installed.
- **A portable build.** `CodeScope-portable.zip` is the application with no
  installer at all, for people who would rather not install anything.
- **Native Windows front end** for the scc analysis engine: a WPF application
  that presents the engine's report natively instead of only as an HTML file.
- **In-app report dashboard** with metric cards, a language distribution bar,
  the line-length histogram, code hotspots, change coupling, the author rollup
  with bus factor, language and author timelines, the COCOMO and LOCOMO cost
  models and a searchable per-file table.
- **Every optional engine section has a real switch.** Turning a section off
  omits it from both the in-app report and the HTML export, through the
  engine's own `--report-skip` flag — the section is absent, not hidden.
- **Sortable tables.** Every table sorts by its column headings, and the sorted
  heading carries an arrow that points the way the rows run. The arrow and the
  row order are driven by the same state, so they cannot disagree.
- **Double-click to reveal.** In the per-file, longest-lines, hotspots and
  coupling tables, a double-click opens the file under the pointer in File
  Explorer.
- **HTML export dialog.** Exporting from the report opens a themed dialog that
  asks for the file name and the folder. Leaving the folder empty writes the
  report beside the analysed code; an existing file is never replaced without
  an explicit confirmation.
- **Analysis history.** Every finished run is stored under
  `%LOCALAPPDATA%\CodeScope\history` with the engine's own report data, so a
  past run can be reopened in the dashboard without running the engine again.
  The last thirty runs are kept.
- **Collapsible sidebar** with the brand, the history list, settings, presets
  and a collapse control, animated and remembered between launches.
- **First-run walkthrough** — four animated steps that explain the application
  and can be skipped at any point.
- **Presets and themes.** Four built-in presets plus your own; dark, light and
  follow-system themes, applied live.
- **Advanced settings** — ignores, extension filters, large-file limits,
  minified and generated detection, language remapping, ULOC, cognitive
  complexity, cost-model parameters, Git window depth and worker tuning.
- **Bundled engine.** A byte-for-byte snapshot of scc 4.1.0 is vendored, with
  both the upstream CLI and a small JSON adapter built from it. No network
  access, no Go toolchain and no scc installation are needed to run the
  application.

### Security

- The analysed codebase is never written to in the in-app mode; the HTML export
  writes exactly one file, to a folder the user chooses.
- Settings and history live in the per-user application-data folder, never
  inside an analysed project.
- The application makes no network request of any kind.

[Unreleased]: https://github.com/JuggyBob/CodeScope/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/JuggyBob/CodeScope/releases/tag/v1.0.0
