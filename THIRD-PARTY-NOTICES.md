# Third-party notices

CodeScope bundles, links against or otherwise uses the work of other people.
This file lists them. Nothing here is covered by this repository's own
licence; each item keeps its own.

---

## scc — the bundled analysis engine

* **Project:** <https://github.com/boyter/scc>
* **Version:** `4.1.0` (`v4.1.0-12-g36b4729`)
* **Commit:** `36b47298e436feec174330a3b7160df61087799f`
* **Copyright:** Copyright (c) 2021 Ben Boyter
* **Licence:** MIT — the full text is in `vendor/scc-source/LICENSE` and is
  shipped inside every release under `vendor\scc-source\LICENSE`.

A byte-for-byte snapshot of the upstream sources is vendored in
`vendor/scc-source/` so the two executables that ship with the application can
be rebuilt and audited. No upstream file is modified.

Two executables are built from that snapshot and shipped in the `backend\`
folder of every release:

| Executable | What it is |
| --- | --- |
| `scc.exe` | The upstream command-line tool, unmodified. It renders the HTML report. |
| `codescope-export.exe` | A small adapter written for this project. It calls scc's own `processor.CollectReportData` and prints the result as JSON. |

### Dependencies vendored inside the snapshot

`scc` vendors its own Go dependencies under `vendor/scc-source/vendor/`. Their
licences are kept alongside their sources, one file per package, and the
notice files required by the Apache-2.0 packages are present too:

| Licence | Packages |
| --- | --- |
| MIT | 18 |
| BSD (2- and 3-clause) | 16 |
| Apache-2.0 | 14 |
| BSD-3-Clause **or** MPL-2.0 (dual) | 1 — `github.com/cyphar/filepath-securejoin`; both licences are shipped, and the files are used unmodified |
| MIT with libyaml-derived files | 1 — `go.yaml.in/yaml/v2`; the libyaml attribution is in `LICENSE.libyaml` |

The MIT licence requires the copyright notice and the permission notice to
travel with every copy. They do: `vendor\scc-source\LICENSE` is part of the
source tree and is copied into every published build.

---

## The .NET runtime and WPF

* **Project:** <https://github.com/dotnet/runtime>
* **Licence:** MIT (the runtime) and the .NET Library License (the Windows
  Desktop libraries, which include WPF)

The application is published **framework-dependent** by default: it does not
ship the runtime and instead requires the .NET 8 Desktop Runtime to be
installed on the target machine. Nothing from the runtime is redistributed in
that build, so no notice for it has to travel with the download.

The self-contained build (`build\publish.ps1 -SelfContained`) does redistribute
the runtime. It is licensed under the MIT licence and the .NET Library License,
both of which permit redistribution of the libraries in object form. The
published folder contains the runtime's own licence and notice files where the
licence requires them.

The **installer** (`CodeScope-Setup.exe`) carries Microsoft's own
`windowsdesktop-runtime-8.0.31-win-x64.exe` and offers to run it when the
machine does not already have the .NET 8 desktop runtime. That file is
Microsoft's, is downloaded from `builds.dotnet.microsoft.com` over HTTPS and is
checked against a pinned SHA-256 before it is packed into the setup. The
runtime it installs is covered by the MIT licence and the .NET Library License
above.

---

## Icons and fonts

The application's icons are original vector paths, drawn for this project and
covered by this repository's licence. The user interface uses the fonts already
present on Windows (Segoe UI, and Cascadia Mono/Consolas for numbers); no font
is redistributed.

---

## Reporting a licensing problem

If you believe something in this list is wrong or incomplete, please open an
issue — or, if it is sensitive, follow [SECURITY.md](SECURITY.md).
