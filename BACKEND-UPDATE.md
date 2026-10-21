# Updating the frozen scc backend

CodeScope pins one exact version of [scc](https://github.com/boyter/scc) and
never talks to a network or to a system-installed copy at runtime. This document
is the procedure for moving that pin forward.

Nothing in this procedure touches the `scc/` checkout in the repository root —
it is only ever read from.

## 1. Pick the version

```powershell
cd <repo-root>\scc
git fetch --tags
git log -1 --format="%H %ci %s"          # the commit you intend to pin
git describe --tags                     # e.g. v4.1.0-12-g36b4729
git status --porcelain                  # must be empty
```

A dirty tree cannot be frozen: record a commit id, not a working directory.

## 2. Re-create the snapshot

```powershell
$root = "<repo-root>\WindowsAPP"
$dst  = "$root\vendor\scc-source"

# Keep the snapshot's own documentation; replace everything else.
Remove-Item -Recurse -Force "$dst\tools" -ErrorAction SilentlyContinue
robocopy "<repo-root>\scc" $dst /MIR /XD .git /NFL /NDL /NJH /NP /R:1 /W:1

# Re-create the adapter (it is not upstream).
New-Item -ItemType Directory -Force -Path "$dst\tools\codescope-export" | Out-Null
Copy-Item "$root\build\adapter\main.go" "$dst\tools\codescope-export\main.go"
```

The adapter's source is kept under `build/adapter/main.go` so that "what
upstream code looks like" and "what CodeScope adds" stay visibly separate.

## 3. Re-record provenance

Update `vendor/scc-source/SNAPSHOT.md` (commit, version, date, file count) and
regenerate the hashes:

```powershell
Push-Location $dst
Get-ChildItem -Recurse -File |
  Where-Object { $_.FullName -notlike '*\tools\*' -and $_.Name -ne 'SNAPSHOT-SUMS.sha256' } |
  Sort-Object FullName |
  ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 $_.FullName).Hash,
                  $_.FullName.Substring((Get-Location).Path.Length + 1)
  } | Out-File -Encoding utf8 SNAPSHOT-SUMS.sha256
Pop-Location
```

## 4. Rebuild both executables

```powershell
powershell -ExecutionPolicy Bypass -File <repo-root>\WindowsAPP\build\build-backend.ps1
```

The script needs a Go toolchain matching `scc/go.mod` (currently `go 1.26.4`).
Pass `-GoRoot` to point it at a portable installation; nothing is downloaded
implicitly.

## 5. Verify the new snapshot

```powershell
powershell -ExecutionPolicy Bypass -File <repo-root>\WindowsAPP\tests\run-tests.ps1
```

The integration suite exercises both executables against throwaway fixtures and
checks the numbers, the section selection, cancellation and the
"no HTML is written in the in-app mode" rule.

## 6. Update the version the UI advertises

`SettingsViewModel.BackendVersionText` and any documentation that quotes a
version number should be refreshed to match `scc.exe --version`.

## Checklist

- [ ] `git status --porcelain` in the root `scc/` checkout is empty
- [ ] no file under `vendor/scc-source` differs from the upstream commit except
      `tools/`, `SNAPSHOT.md` and `SNAPSHOT-SUMS.sha256`
- [ ] `scc.exe --version` and `codescope-export.exe --version` agree
- [ ] `SNAPSHOT.md` records the new commit, version and date
- [ ] the integration suite passes
- [ ] the root `scc/` checkout is still clean and on its original branch