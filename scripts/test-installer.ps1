[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [switch]$AllowCurrentUserChanges
)

$ErrorActionPreference = 'Stop'
if (-not $AllowCurrentUserChanges) { throw 'Run only in a disposable Windows account with -AllowCurrentUserChanges. This installs and uninstalls PowerMouse.' }
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\PowerMouse'
$dataDir = Join-Path $env:LOCALAPPDATA 'PowerMouse'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\PowerMouse.lnk'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B1293760-1F68-47D9-9000-F2B4071C66AE}_is1'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
function Get-PowerMouseStartupEntry {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    if ($null -eq $key) { return $null }
    try { $key.GetValue('PowerMouse', $null) }
    finally { $key.Dispose() }
}
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$taskName = "PowerMouse Startup $sid"
foreach ($path in @($installDir, $dataDir, $shortcut, $uninstallKey)) {
    if (Test-Path -LiteralPath $path) { throw "Refusing to touch an existing installation or data: $path" }
}
if (Get-Process PowerMouse -ErrorAction SilentlyContinue) { throw 'PowerMouse is already running. Use a disposable account.' }
if ($null -ne (Get-PowerMouseStartupEntry)) { throw 'Existing startup entry found.' }
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { throw 'Existing startup task found.' }
$version = ([string][Diagnostics.FileVersionInfo]::GetVersionInfo($installer).ProductVersion).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'The installer does not contain a valid release version.' }
$logDir = Join-Path (Split-Path -Parent $installer) 'installer-test'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

function Invoke-Setup([string]$Executable, [string]$LogName) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $logDir $LogName) + '"'))
    $process = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$LogName failed with exit code $($process.ExitCode)." }
}

Invoke-Setup $installer 'install.log'
$registration = Get-ItemProperty -LiteralPath $uninstallKey
if ($registration.DisplayVersion -ne $version -or $registration.DisplayName -ne "PowerMouse $version" -or
    $registration.Publisher -ne 'PowerMouse contributors') { throw 'Installed metadata does not match the manifest.' }
foreach ($required in @('PowerMouse.exe', 'PowerMouse.dll', 'PowerMouse.pri', 'App.xbf', 'unins000.exe', 'uninstall-startup.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $installDir $required))) { throw "Installer omitted $required." }
}
if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Start menu shortcut is missing.' }
if (Get-Process PowerMouse -ErrorAction SilentlyContinue) { throw 'Silent installation unexpectedly launched PowerMouse.' }

# Verify reinstall and uninstall preserve user data, and uninstall removes both startup mechanisms.
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$settings = Join-Path $dataDir 'settings.json'
[IO.File]::WriteAllText($settings, '{"releaseTestSentinel":true}')
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$executable = Join-Path $installDir 'PowerMouse.exe'
if (-not (Test-Path -LiteralPath $runKey)) { New-Item -Path $runKey -Force | Out-Null }
New-ItemProperty -LiteralPath $runKey -Name PowerMouse -Value ('"' + $executable + '"') -PropertyType String -Force | Out-Null
$action = New-ScheduledTaskAction -Execute $executable -WorkingDirectory $installDir
$principal = New-ScheduledTaskPrincipal -UserId $sid -LogonType Interactive -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal | Out-Null
Invoke-Setup $installer 'reinstall.log'
if ((Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash) { throw 'Reinstall modified settings.' }
if (-not (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue)) { throw 'Reinstall removed startup preferences.' }
Invoke-Setup (Join-Path $installDir 'unins000.exe') 'uninstall.log'
foreach ($path in @($uninstallKey, $shortcut, $executable)) {
    if (Test-Path -LiteralPath $path) { throw "Uninstall left behind $path." }
}
if ($null -ne (Get-PowerMouseStartupEntry)) { throw 'Uninstall left the startup registry entry.' }
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { throw 'Uninstall left the startup task.' }
if ((Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash) { throw 'Uninstall changed user settings.' }
Write-Host 'Silent install, metadata, shortcut, reinstall, startup cleanup, and data preservation passed.'
