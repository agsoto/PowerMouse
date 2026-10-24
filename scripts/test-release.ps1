[CmdletBinding()]
param([string]$ReleaseDirectory)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = & (Join-Path $PSScriptRoot 'get-release-version.ps1')
if (-not $ReleaseDirectory) { $ReleaseDirectory = Join-Path $repoRoot "artifacts\release\$version" }
$expectedNames = @("PowerMouse-$version-win-x64-setup.exe", "PowerMouse-$version-win-x64.zip")
$lines = @(Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS.txt'))
if ($lines.Count -ne $expectedNames.Count) { throw 'The checksum list must contain exactly the installer and ZIP.' }
foreach ($name in $expectedNames) {
    $path = Join-Path $ReleaseDirectory $name
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected = "$hash  $name"
    if ($lines -cnotcontains $expected -or (Get-Content -LiteralPath "$path.sha256" -Raw).Trim() -cne $expected) {
        throw "Checksum mismatch for $name."
    }
}
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseDirectory $expectedNames[1]))
try {
    $names = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($required in @('PowerMouse.exe', 'PowerMouse.dll', 'PowerMouse.Core.dll', 'PowerMouse.ClickHints.dll', 'PowerMouse.ClickHints.Windows.dll', 'PowerMouse.pri', 'App.xbf', 'LICENSE', 'README.md', 'CHANGELOG.md')) {
        if ($names -cnotcontains $required) { throw "Archive is missing $required." }
    }
    if ($names | Where-Object { $_ -match '(^|/)(\.\.|\.git|settings\.json|unins\d*\.exe)(/|$)' -or $_ -match '^[/\\]|^[A-Za-z]:' }) {
        throw 'Archive contains an unsafe path or local installation state.'
    }
    $stream = $zip.GetEntry('PowerMouse.exe').Open()
    $buffer = [IO.MemoryStream]::new()
    try {
        $stream.CopyTo($buffer)
        $bytes = $buffer.ToArray()
        $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
        if ([BitConverter]::ToUInt16($bytes, $peOffset + 4) -ne 0x8664) { throw 'Archive executable is not x64.' }
    }
    finally { $stream.Dispose(); $buffer.Dispose() }
}
finally { $zip.Dispose() }
$manifestDir = Join-Path $ReleaseDirectory "winget\manifests\a\AGSoto\PowerMouse\$version"
$installerManifest = Get-Content -LiteralPath (Join-Path $manifestDir 'AGSoto.PowerMouse.installer.yaml') -Raw
$installerHash = (Get-FileHash -LiteralPath (Join-Path $ReleaseDirectory $expectedNames[0]) -Algorithm SHA256).Hash
if ($installerManifest -notmatch "(?m)^  InstallerSha256: $installerHash\r?$" -or
    $installerManifest -notmatch "(?m)^  InstallerUrl: https://github.com/agsoto/PowerMouse/releases/download/v$([regex]::Escape($version))/$([regex]::Escape($expectedNames[0]))\r?$") {
    throw 'WinGet manifest does not reference this exact release installer.'
}
winget validate --manifest $manifestDir --disable-interactivity
if ($LASTEXITCODE -ne 0) { throw "WinGet validation failed with exit code $LASTEXITCODE." }
Write-Host 'Release checksums, archive contents, x64 executable, and WinGet manifests passed.'
