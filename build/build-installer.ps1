<#
.SYNOPSIS
    Builds the CodeScope installer (a single Setup.exe) with Inno Setup.

.DESCRIPTION
    Publishes the application first, then compiles build\installer\CodeScope.iss
    into a Setup.exe. The installer installs the published application for the
    current user — no administrator rights are needed — and adds a Start menu
    shortcut and a proper uninstaller.

    The published files and the installer are written to the output folder,
    which defaults to the PublishExecutables folder next to this repository.

    Requires Inno Setup 6. Install it with:
      winget install --id JRSoftware.InnoSetup --exact --scope user

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\build-installer.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\build-installer.ps1 -OutputDir D:\releases
#>
[CmdletBinding()]
param(
    [string] $OutputDir,
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'

# Where this script lives. $PSScriptDir is not always populated when the
# script is started in a particular way, so there is a fallback chain.
$scriptDir = $PSScriptDir
if (-not $scriptDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if (-not $scriptDir) {
    $scriptDir = (Get-Location).Path
}

$appRoot = Split-Path -Parent $scriptDir
$project = Join-Path $appRoot 'src\CodeScope.App\CodeScope.App.csproj'
$publish = Join-Path $appRoot 'publish\CodeScope'
$script = Join-Path $appRoot 'build\installer\CodeScope.iss'

# The .NET 8 desktop runtime installer that the setup can offer to install.
# It is downloaded from Microsoft's build server and checked against a pinned
# hash, so a truncated or substituted download cannot slip in.
$runtimeVersion = '8.0.31'
$runtimeName = "windowsdesktop-runtime-$runtimeVersion-win-x64.exe"
$runtimeUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$runtimeVersion/$runtimeName"
$runtimeSha256 = 'c375dfd80a967405cfeff634912c1fccc56261ce3d9209ea473f60a21184d1cc'
$runtimeDir = Join-Path $appRoot 'build\installer\runtime'
$runtimeFile = Join-Path $runtimeDir $runtimeName

if (-not $OutputDir) {
    # A PublishExecutables folder beside the repository.
    $OutputDir = Join-Path (Split-Path -Parent $appRoot) 'PublishExecutables'
}

function Find-InnoCompiler {
    $candidates = @(
        $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe' }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe' }),
        $(if ([Environment]::GetFolderPath('ProgramFilesX86')) { Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6\ISCC.exe' })
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    $onPath = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    return $null
}

$iscc = Find-InnoCompiler
if (-not $iscc) {
    throw 'Inno Setup 6 was not found. Install it with: winget install --id JRSoftware.InnoSetup --exact --scope user'
}

Write-Host "`n== Publishing the application ==" -ForegroundColor Cyan
$publishArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $appRoot 'build\publish.ps1'), '-Configuration', $Configuration, '-Runtime', $Runtime)
if ($SelfContained) {
    $publishArgs += '-SelfContained'
}
& powershell @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "The publish step failed (exit $LASTEXITCODE)."
}

if (-not (Test-Path (Join-Path $publish 'CodeScope.exe'))) {
    throw "The published application is missing from $publish."
}

Write-Host "`n== Compiling the installer ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Make sure the runtime installer the setup can offer is there and intact.
if (-not (Test-Path $runtimeFile)) {
    Write-Host "  downloading the .NET 8 desktop runtime installer..." -ForegroundColor DarkGray
    $ProgressPreference = 'SilentlyContinue'
    New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null
    Invoke-WebRequest -Uri $runtimeUrl -OutFile $runtimeFile -TimeoutSec 900 -UseBasicParsing
}

$actualHash = (Get-FileHash $runtimeFile -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $runtimeSha256) {
    throw "The runtime installer does not match the pinned hash. Expected $runtimeSha256, got $actualHash."
}
Write-Host "  runtime installer: $runtimeName ($([math]::Round((Get-Item $runtimeFile).Length / 1MB, 1)) MB, hash verified)"

& $iscc "/O$OutputDir" $script
if ($LASTEXITCODE -ne 0) {
    throw "The Inno Setup compiler failed (exit $LASTEXITCODE)."
}

$setup = Join-Path $OutputDir 'CodeScope-Setup.exe'
if (-not (Test-Path $setup)) {
    throw 'The installer was not produced.'
}

# The portable build goes alongside it, for people who would rather not
# install anything.
$portable = Join-Path $OutputDir 'CodeScope-portable.zip'
if (Test-Path $portable) {
    Remove-Item $portable -Force
}
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portable -CompressionLevel Optimal

Write-Host "`nPublished to $OutputDir" -ForegroundColor Green
Write-Host "  installer : CodeScope-Setup.exe  ($([math]::Round((Get-Item $setup).Length / 1MB, 1)) MB)"
Write-Host "  portable  : CodeScope-portable.zip  ($([math]::Round((Get-Item $portable).Length / 1MB, 1)) MB)"
