[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\PowerMouse'),

    [switch]$PurgeData,

    [switch]$ForceStop
)

$ErrorActionPreference = 'Stop'
$installFullPath = [System.IO.Path]::GetFullPath($InstallPath)
$installName = Split-Path -Leaf $installFullPath
$backupPath = Join-Path (Split-Path -Parent $installFullPath) "$installName.previous"
$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\PowerMouse.lnk'
$dataPath = Join-Path $env:LOCALAPPDATA 'PowerMouse'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

function Assert-SafeRemovalDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $rootPath = [System.IO.Path]::GetPathRoot($fullPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $protectedPaths = @(
        $rootPath,
        [System.IO.Path]::GetFullPath($env:USERPROFILE).TrimEnd([System.IO.Path]::DirectorySeparatorChar),
        [System.IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd([System.IO.Path]::DirectorySeparatorChar),
        [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs')).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    )

    if ([string]::IsNullOrWhiteSpace((Split-Path -Leaf $fullPath)) -or $protectedPaths -contains $fullPath) {
        throw "Refusing to remove broad or protected directory '$fullPath'."
    }
}

function Get-RunningPowerMouse {
    @(Get-Process -Name 'PowerMouse' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
}

Assert-SafeRemovalDirectory -Path $installFullPath
Assert-SafeRemovalDirectory -Path $backupPath
if ($PurgeData) {
    Assert-SafeRemovalDirectory -Path $dataPath
}

if (-not $PSCmdlet.ShouldProcess($installFullPath, 'Uninstall the PowerMouse developer installation')) {
    return
}

$installedExecutable = Join-Path $installFullPath 'PowerMouse.exe'
if (Test-Path -LiteralPath $installedExecutable -PathType Leaf) {
    $shutdownProcess = Start-Process -FilePath $installedExecutable -ArgumentList '--shutdown' -WindowStyle Hidden -Wait -PassThru
    if ($shutdownProcess.ExitCode -ne 0) {
        Write-Warning "The shutdown command exited with code $($shutdownProcess.ExitCode)."
    }
}

$deadline = [DateTime]::UtcNow.AddSeconds(8)
do {
    $running = Get-RunningPowerMouse
    if ($running.Count -eq 0) {
        break
    }

    Start-Sleep -Milliseconds 200
} while ([DateTime]::UtcNow -lt $deadline)

if ($running.Count -gt 0) {
    if (-not $ForceStop) {
        $processIds = ($running | ForEach-Object { $_.Id }) -join ', '
        throw "PowerMouse is still running (PID $processIds). Close it from the tray and retry, or pass -ForceStop."
    }

    foreach ($process in $running) {
        Stop-Process -Id $process.Id -Force
    }
}

# Remove only this user's PowerMouse task before deleting its executable. An
# access error must stop uninstall rather than leave an elevated startup entry.
$startupSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$startupTaskName = "PowerMouse Startup $startupSid"
$scheduler = New-Object -ComObject 'Schedule.Service'
try {
    $scheduler.Connect()
    $taskFolder = $scheduler.GetFolder('\')
    try {
        $startupTask = $null
        try { $startupTask = $taskFolder.GetTask($startupTaskName) }
        catch {
            if ($_.Exception.HResult -ne -2147024894 -and $_.Exception.InnerException.HResult -ne -2147024894) { throw }
        }
        if ($null -ne $startupTask) {
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($startupTask)
            $taskFolder.DeleteTask($startupTaskName, 0)
        }
    }
    finally {
        if ($null -ne $taskFolder) { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskFolder) }
    }
}
finally { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($scheduler) }

if (Test-Path -LiteralPath $installFullPath) {
    Remove-Item -LiteralPath $installFullPath -Recurse -Force
}
if (Test-Path -LiteralPath $backupPath) {
    Remove-Item -LiteralPath $backupPath -Recurse -Force
}
if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}
if (Test-Path -LiteralPath $runKeyPath) {
    Remove-ItemProperty -LiteralPath $runKeyPath -Name 'PowerMouse' -ErrorAction SilentlyContinue
}
if ($PurgeData -and (Test-Path -LiteralPath $dataPath)) {
    Remove-Item -LiteralPath $dataPath -Recurse -Force
}

if ($PurgeData) {
    Write-Host 'PowerMouse, its settings, and its logs were removed.'
}
else {
    Write-Host "PowerMouse was removed. Settings and logs remain at $dataPath."
}
