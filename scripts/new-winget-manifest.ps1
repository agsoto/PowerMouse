[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [string]$OutputDirectory,
    [switch]$VerifyDownload
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = & (Join-Path $PSScriptRoot 'get-release-version.ps1')
$packageId = 'AGSoto.PowerMouse'
$installerName = "PowerMouse-$version-win-x64-setup.exe"
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.Name -cne $installerName) { throw "Expected installer filename $installerName." }
if ($installer.VersionInfo.ProductName.Trim() -ne 'PowerMouse' -or
    $installer.VersionInfo.ProductVersion.Trim() -ne $version -or
    $installer.VersionInfo.FileVersion.Trim() -ne "$version.0") {
    throw 'Installer product/version metadata does not match the release.'
}
$hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash
$url = "https://github.com/agsoto/PowerMouse/releases/download/v$version/$installerName"
if ($VerifyDownload) {
    $downloadPath = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    try {
        Invoke-WebRequest -Uri $url -OutFile $downloadPath
        if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $hash) {
            throw 'The public release installer differs from the local installer. Do not submit this manifest.'
        }
    }
    finally { if (Test-Path -LiteralPath $downloadPath) { Remove-Item -LiteralPath $downloadPath -Force } }
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts\winget' }
$manifestDirectory = Join-Path $OutputDirectory "manifests\a\AGSoto\PowerMouse\$version"
New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null
$files = @{
    "$packageId.yaml" = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.12.0
"@
    "$packageId.installer.yaml" = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $version
MinimumOSVersion: 10.0.22000.0
InstallerType: inno
Scope: user
InstallModes:
- interactive
- silent
- silentWithProgress
UpgradeBehavior: install
ProductCode: '{B1293760-1F68-47D9-9000-F2B4071C66AE}_is1'
AppsAndFeaturesEntries:
- DisplayName: PowerMouse $version
  Publisher: PowerMouse contributors
  DisplayVersion: $version
  ProductCode: '{B1293760-1F68-47D9-9000-F2B4071C66AE}_is1'
Installers:
- Architecture: x64
  InstallerUrl: $url
  InstallerSha256: $hash
ManifestType: installer
ManifestVersion: 1.12.0
"@
    "$packageId.locale.en-US.yaml" = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $version
PackageLocale: en-US
Publisher: PowerMouse contributors
PublisherUrl: https://github.com/agsoto/PowerMouse
PublisherSupportUrl: https://github.com/agsoto/PowerMouse/issues
PackageName: PowerMouse
PackageUrl: https://github.com/agsoto/PowerMouse
License: MIT
LicenseUrl: https://github.com/agsoto/PowerMouse/blob/v$version/LICENSE
Copyright: Copyright (c) 2026 PowerMouse contributors
ShortDescription: Keyboard-first pointer control for Windows.
Description: |-
  Move, click, drag, and scroll with configurable keyboard controls, profiles,
  acceleration curves, and an optional pointer indicator. Experimental Mouse
  Hints labels visible controls with short codes. Settings and logs stay local.
Moniker: powermouse
Tags:
- keyboard
- mouse
- productivity
- windows
ReleaseNotesUrl: https://github.com/agsoto/PowerMouse/releases/tag/v$version
ManifestType: defaultLocale
ManifestVersion: 1.12.0
"@
}
foreach ($name in $files.Keys) {
    [IO.File]::WriteAllText((Join-Path $manifestDirectory $name), $files[$name].Replace("`r`n", "`n") + "`n", [Text.UTF8Encoding]::new($false))
}
Write-Host "WinGet manifests: $manifestDirectory"
if (-not $VerifyDownload) { Write-Host 'Prepared locally. Verify the public release download before submitting.' }
$manifestDirectory
