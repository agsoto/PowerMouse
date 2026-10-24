[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$properties = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup
$version = [string]$properties.Version
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw 'Stable releases require a numeric major.minor.patch version in Directory.Build.props.'
}
if ($properties.AssemblyVersion -ne "$version.0" -or $properties.FileVersion -ne "$version.0") {
    throw 'Version, AssemblyVersion, and FileVersion must match in Directory.Build.props.'
}
if (-not (Select-String -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Pattern "^## $([regex]::Escape($version))( |$)" -Quiet)) {
    throw "CHANGELOG.md does not have a section for $version."
}
$version
