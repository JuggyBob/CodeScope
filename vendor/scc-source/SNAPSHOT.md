# Frozen scc snapshot

CodeScope never builds against, modifies or depends on the `scc` repository that
lives next to it. Everything the application needs is copied into
`WindowsAPP/vendor/scc-source/` and frozen there.

## Provenance

| Field | Value |
| --- | --- |
| Upstream | https://github.com/boyter/scc |
| Commit | `36b47298e436feec174330a3b7160df61087799f` |
| Version reported by the engine | `4.1.0` |
| `git describe` | `v4.1.0-12-g36b4729` |
| Snapshot taken | 2026-10-14 |
| Files | 2,010 (the upstream working tree excluding `.git/`) |
| License | MIT — see `LICENSE` in this folder |

`SNAPSHOT-SUMS.sha256` records a SHA-256 for every upstream file in the snapshot.
Regenerate and diff it after any snapshot replacement to prove the engine was
not edited:

```powershell
# from WindowsAPP/vendor/scc-source
Get-ChildItem -Recurse -File |
  Where-Object { $_.FullName -notlike '*\tools\*' -and $_.Name -ne 'SNAPSHOT-SUMS.sha256' } |
  Sort-Object FullName |
  ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 $_.FullName).Hash,
                  $_.FullName.Substring((Get-Location).Path.Length + 1)
  } | Out-File -Encoding utf8 SNAPSHOT-SUMS.sha256
```

## What is in here

* Every upstream source file, embedded asset, language definition
  (`languages.json`), build dependency (`vendor/`), licence and helper script,
  byte-for-byte as committed.
* **One addition:** `tools/codescope-export/`, documented below. No upstream file
  is touched, renamed or removed.

## The two executables

Both are produced by `build/build-backend.ps1` from this snapshot with Go 1.26.4
(the version `go.mod` requires) and no network access:

```
backend\scc.exe             the upstream CLI, unmodified
backend\codescope-export.exe  CodeScope's data adapter (see below)
```

### scc.exe

`go build -trimpath -ldflags "-s -w" .` — the upstream `main` package, exactly as
shipped. It is what writes the HTML infographic (`--report`), what answers
`--version`, and what a user can run by hand from the install folder.

### codescope-export.exe

`go build -trimpath -ldflags "-s -w" ./tools/codescope-export`

scc's own report path renders every infographic section into one self-contained
HTML document, and the CLI's structured outputs do not cover everything the
infographic contains:

* `--format json` / `--format json2` drop the line-length histogram and the
  per-language ULOC rollup.
* No JSON format exposes the git-history tables (hotspots, coupling, authors,
  language and author timelines); those only exist as separate CLI reports that
  are mutually exclusive per invocation.

Rather than approximate those numbers in C#, `codescope-export` is a small
`package main` that sets the frozen engine's exported configuration flags and
calls `processor.CollectReportData(path)` — **the very same function scc's
`--report` calls** — then prints the resulting `ReportData` as JSON.

Consequences:

* Every number the CodeScope dashboard shows comes from the frozen engine's own
  code path, not from a re-implementation.
* One process, one pass, no intermediate files. In the in-app mode nothing is
  written to disk at all.
* The flags it accepts mirror the upstream flags one-for-one (`--uloc`,
  `--dryness`, `--no-large`, `--avg-wage`, …), so the GUI builds a single
  argument list that is valid for both executables.
* The flag implications that upstream keeps in the unexported
  `processor.processFlags()` (`--dryness` implies `--uloc`, `--no-min` implies
  `--min`, …) are mirrored in `applyOptions` with a comment pointing at the
  original, so both executables interpret a flag identically.

Run `backend\codescope-export.exe --help` for its flag surface.

## Replacing the snapshot

See `BACKEND-UPDATE.md` in the repository root for the full procedure.