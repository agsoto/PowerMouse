[CmdletBinding()]
param(
    [string]$CompilerPath,
    [switch]$SkipTests,
    [switch]$SkipInteractiveTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = & (Join-Path $PSScriptRoot 'get-release-version.ps1')
if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path $repoRoot '.tools\inno-setup\ISCC.exe'),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $CompilerPath) { $CompilerPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Install Inno Setup 6.7.3, or pass -CompilerPath with the path to ISCC.exe. See RELEASING.md.'
}
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
$releaseDir = Join-Path $repoRoot "artifacts\release\$version"
# A fresh publish directory prevents obsolete files from leaking into a release.
$publishDir = Join-Path $repoRoot ("artifacts\release-build-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -Platform x64 -OutputPath $publishDir -SkipTests:$SkipTests -SkipInteractiveTests:$SkipInteractiveTests

foreach ($required in @('PowerMouse.exe', 'PowerMouse.dll', 'PowerMouse.Core.dll', 'PowerMouse.ClickHints.dll', 'PowerMouse.ClickHints.Windows.dll', 'PowerMouse.pri', 'App.xbf', 'LICENSE', 'README.md', 'CHANGELOG.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $required) -PathType Leaf)) { throw "Publish output is missing $required." }
}
if ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $publishDir 'PowerMouse.dll')).FileVersion -ne "$version.0") {
    throw 'Application version does not match the release version.'
}
$archivePath = Join-Path $releaseDir "PowerMouse-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $archivePath -CompressionLevel Optimal -Force
& $CompilerPath '/Qp' "/DAppVersion=$version" "/DPublishDir=$publishDir" "/DReleaseDir=$releaseDir" (Join-Path $repoRoot 'packaging\PowerMouse.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed with exit code $LASTEXITCODE." }
$installerPath = Join-Path $releaseDir "PowerMouse-$version-win-x64-setup.exe"
$checksumLines = foreach ($file in @($installerPath, $archivePath)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $line = "$hash  $([IO.Path]::GetFileName($file))"
    [IO.File]::WriteAllText("$file.sha256", "$line`n")
    $line
}
[IO.File]::WriteAllText((Join-Path $releaseDir 'SHA256SUMS.txt'), ($checksumLines -join "`n") + "`n")
& (Join-Path $PSScriptRoot 'new-winget-manifest.ps1') -InstallerPath $installerPath -OutputDirectory (Join-Path $releaseDir 'winget') | Out-Null
Write-Host "Release prepared at $releaseDir"
Write-Host "Published application files: $publishDir"
