[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$toolsDir = Join-Path $repoRoot '.tools'
$compilerDir = Join-Path $toolsDir 'inno-setup'
New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
$installer = Join-Path $toolsDir 'innosetup-6.7.3.exe'
$expectedHash = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
if (-not (Test-Path -LiteralPath $installer) -or
    (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $expectedHash) {
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $installer
}
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'The Inno Setup installer does not match the pinned SHA-256 checksum.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Pyrsys B.V.*') {
    throw 'The Inno Setup installer does not have the expected valid publisher signature.'
}
$arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', '/TYPE=custom', '/COMPONENTS=compiler', '/MERGETASKS=!fileassoc', ('/DIR="' + $compilerDir + '"'))
$process = Start-Process -FilePath $installer -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Inno Setup installation failed: $($process.ExitCode)." }
Write-Host "Release compiler: $compilerDir\ISCC.exe"
