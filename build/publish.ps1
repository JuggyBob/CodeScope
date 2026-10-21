<#
.SYNOPSIS
    Publishes CodeScope as a folder that runs on Windows with no Go toolchain
    and no network access.

.DESCRIPTION
    Produces WindowsAPP\publish\CodeScope\ containing the application, the two
    frozen executables and the snapshot provenance documentation.

    By default the build is framework-dependent: the target machine needs the
    .NET 8 desktop runtime installed, and the result is about thirty megabytes.
    Pass -SelfContained to ship the whole runtime with the application, so it
    runs on a clean Windows machine with nothing installed, at the cost of a
    much bigger download.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\publish.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\publish.ps1 -SelfContained
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $appRoot 'src\CodeScope.App\CodeScope.App.csproj'
$output = Join-Path $appRoot 'publish\CodeScope'

$backend = Join-Path $appRoot 'backend'
foreach ($exe in 'scc.exe', 'codescope-export.exe') {
    if (-not (Test-Path (Join-Path $backend $exe))) {
        throw "Missing backend\$exe. Run build\build-backend.ps1 first."
    }
}

if (Test-Path $output) { Remove-Item -Recurse -Force $output }

# By default the build asks for the .NET 8 desktop runtime to be installed on
# the target machine, which keeps the download to about thirty megabytes.
# -SelfContained ships the whole runtime instead, so the app runs on a clean
# Windows machine with nothing installed, at the cost of a much bigger
# download.
Write-Host "`n== Publishing CodeScope ($Runtime, self-contained=$([bool]$SelfContained)) ==" -ForegroundColor Cyan

$args = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', $Runtime,
    '-o', $output,
    '--self-contained', ([bool]$SelfContained).ToString().ToLowerInvariant(),
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=false',
    '-p:DebugType=none',
    '--nologo'
)

if ($SelfContained) {
    # The runtime is the bulk of a self-contained build, so it is compressed
    # into the single executable. A framework-dependent build has nothing
    # large enough to compress.
    $args += '-p:EnableCompressionInSingleFile=true'
}

& dotnet @args
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

# The csproj copies these next to the app; make the guarantee explicit.
New-Item -ItemType Directory -Force -Path (Join-Path $output 'backend') | Out-Null
Copy-Item (Join-Path $backend '*.exe') (Join-Path $output 'backend') -Force

Write-Host "`nPublished to $output" -ForegroundColor Green
Write-Host "  size: $([math]::Round((Get-ChildItem -Recurse -File $output | Measure-Object Length -Sum).Sum / 1MB, 1)) MB"
Write-Host "  scc.exe --version: $(& (Join-Path $output 'backend\scc.exe') --version)"