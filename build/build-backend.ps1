<#
.SYNOPSIS
    Builds the frozen scc backend that ships with CodeScope.

.DESCRIPTION
    Compiles two executables from the pinned snapshot in vendor\scc-source:

      scc.exe               the upstream CLI, byte-identical sources
      codescope-export.exe  CodeScope's data adapter over the frozen engine

    Nothing is downloaded: point -GoRoot at a Go toolchain whose version
    satisfies vendor\scc-source\go.mod (Go 1.26.4 at the time of writing).

.PARAMETER GoRoot
    GOROOT of a Go installation. Falls back to $env:GOROOT, then to whatever
    "go" is on PATH.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\build-backend.ps1
    powershell -ExecutionPolicy Bypass -File build\build-backend.ps1 -GoRoot C:\tools\go
#>
[CmdletBinding()]
param(
    [string] $GoRoot,
    [string] $SnapshotDir,
    [string] $BackendDir
)

$ErrorActionPreference = 'Stop'

$appRoot = Split-Path -Parent $PSScriptRoot
if (-not $SnapshotDir) { $SnapshotDir = Join-Path $appRoot 'vendor\scc-source' }
if (-not $BackendDir)  { $BackendDir  = Join-Path $appRoot 'backend' }

function Resolve-Go {
    param([string] $Root)

    if ($Root) {
        $exe = Join-Path $Root 'bin\go.exe'
        if (Test-Path $exe) { return @{ Exe = $exe; Home = $Root } }
        throw "No go.exe under -GoRoot '$Root'."
    }

    if ($env:GOROOT -and (Test-Path (Join-Path $env:GOROOT 'bin\go.exe'))) {
        return @{ Exe = (Join-Path $env:GOROOT 'bin\go.exe'); Home = $env:GOROOT }
    }

    $onPath = Get-Command go -ErrorAction SilentlyContinue
    if ($onPath) {
        return @{ Exe = $onPath.Source; Home = Split-Path -Parent (Split-Path -Parent $onPath.Source) }
    }

    throw @'
No Go toolchain found. Install Go (see https://go.dev/dl/) or pass -GoRoot
pointing at a portable extraction. The version must satisfy the snapshot's
go.mod directive.
'@
}

if (-not (Test-Path (Join-Path $SnapshotDir 'go.mod'))) {
    throw "No snapshot found at '$SnapshotDir'. See BACKEND-UPDATE.md."
}

$go = Resolve-Go -Root $GoRoot
Write-Host "Go toolchain : $($go.Exe)"
& $go.Exe version | ForEach-Object { Write-Host "               $_" }

$cache = Join-Path $env:LOCALAPPDATA 'CodeScope\build\gocache'
New-Item -ItemType Directory -Force -Path $cache, $BackendDir | Out-Null

# Vendor everything: the snapshot must build with no network access.
$env:GOROOT          = $go.Home
$env:GOFLAGS         = '-mod=vendor'
$env:CGO_ENABLED     = '0'
$env:GOCACHE         = $cache
$env:GOPATH          = Join-Path $env:LOCALAPPDATA 'CodeScope\build\gopath'
$env:GOPROXY         = 'off'
$env:GOTOOLCHAIN     = 'local'

Write-Host "`nBuilding scc.exe from the frozen snapshot ..."
Push-Location $SnapshotDir
try {
    & $go.Exe build -trimpath -ldflags '-s -w' -o (Join-Path $BackendDir 'scc.exe') .
    if ($LASTEXITCODE -ne 0) { throw "go build failed for scc.exe (exit $LASTEXITCODE)." }

    Write-Host "Building codescope-export.exe (data adapter) ..."
    & $go.Exe build -trimpath -ldflags '-s -w' -o (Join-Path $BackendDir 'codescope-export.exe') ./tools/codescope-export
    if ($LASTEXITCODE -ne 0) { throw "go build failed for codescope-export.exe (exit $LASTEXITCODE)." }
}
finally {
    Pop-Location
}

$engine  = & (Join-Path $BackendDir 'scc.exe') --version
$adapter = & (Join-Path $BackendDir 'codescope-export.exe') --version

Write-Host "`n  scc.exe              $engine"
Write-Host "  codescope-export.exe $adapter"

if ($engine.Trim() -ne $adapter.Trim()) {
    throw "Version mismatch between the two bundled executables: '$engine' vs '$adapter'."
}

Write-Host "`nBackend ready in $BackendDir" -ForegroundColor Green