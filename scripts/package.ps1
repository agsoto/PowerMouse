[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Join-Path $repoRoot "artifacts\PowerMouse-$Platform"
$version = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$architecture = $Platform.ToLowerInvariant()
$archiveName = "PowerMouse-$version-win-$architecture.zip"
$archivePath = Join-Path $repoRoot "artifacts\$archiveName"

& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -Platform $Platform -SkipTests:$SkipTests

if (Test-Path $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $archivePath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$archivePath.sha256", "$hash  $archiveName`n")
Write-Host "Package created at $archivePath"

