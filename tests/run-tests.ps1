<#
.SYNOPSIS
    Runs the CodeScope test suite (unit + integration).

.DESCRIPTION
    Builds the solution if needed, then runs the dependency-free console suite.
    The integration tests drive the two executables in backend\ against
    throwaway fixtures, so they verify the shipped engine rather than a mock.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1
    powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $appRoot 'CodeScope.sln'
$tests = Join-Path $PSScriptRoot 'CodeScope.Tests\CodeScope.Tests.csproj'

Write-Host "`n== Building ==" -ForegroundColor Cyan
& dotnet build $solution -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }

Write-Host "`n== Testing ==" -ForegroundColor Cyan
& dotnet run --project $tests -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit $LASTEXITCODE)." }

Write-Host "`nAll checks passed." -ForegroundColor Green