[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Join-Path $repoRoot "artifacts\PowerMouse-$Platform"
$archivePath = Join-Path $repoRoot "artifacts\PowerMouse-$Platform.zip"

& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -Platform $Platform

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $archivePath -CompressionLevel Optimal
Write-Host "Package created at $archivePath"

