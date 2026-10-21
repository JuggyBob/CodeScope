<#
.SYNOPSIS
    Builds CodeScope and runs its test suite.

.DESCRIPTION
    Restores, builds and runs the integration suite. The suite drives the two
    frozen executables in backend\ against throwaway fixtures, so it verifies
    the shipped engine rather than a mock.

    Use -SkipBackend to skip the integration tests when only the GUI changed,
    or -RebuildBackend to rebuild the executables first (needs a Go toolchain).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\build.ps1
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $SkipBackend,
    [switch] $RebuildBackend
)

$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $appRoot 'CodeScope.sln'
$tests = Join-Path $appRoot 'tests\CodeScope.Tests\CodeScope.Tests.csproj'

if ($RebuildBackend) {
    & (Join-Path $PSScriptRoot 'build-backend.ps1')
}

Write-Host "`n== Building $solution ($Configuration) ==" -ForegroundColor Cyan
& dotnet build $solution -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }

if ($SkipBackend) {
    Write-Host "`n== Skipping integration tests (-SkipBackend) ==" -ForegroundColor Yellow
    exit 0
}

Write-Host "`n== Running the test suite ==" -ForegroundColor Cyan
& dotnet run --project $tests -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit $LASTEXITCODE)." }

Write-Host "`nAll checks passed." -ForegroundColor Green