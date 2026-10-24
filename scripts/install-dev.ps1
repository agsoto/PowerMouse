[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('Auto', 'x64', 'ARM64')]
    [string]$Platform = 'Auto',

    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'Programs\PowerMouse'),

    [switch]$SkipTests,

    # Reuse artifacts/PowerMouse-<platform> after local tests and visual checks.
    [switch]$UsePublishedBuild,

    [switch]$NoStart,

    [switch]$ForceStop
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$installFullPath = [System.IO.Path]::GetFullPath($InstallPath)
$installParent = Split-Path -Parent $installFullPath
$installName = Split-Path -Leaf $installFullPath
$stagingPath = Join-Path $installParent "$installName.next-$PID"
$backupPath = Join-Path $installParent "$installName.previous"
$startMenuPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenuPath 'PowerMouse.lnk'

function Assert-SafeInstallDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $rootPath = [System.IO.Path]::GetPathRoot($fullPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $protectedPaths = @(
        $rootPath,
        [System.IO.Path]::GetFullPath($env:USERPROFILE).TrimEnd([System.IO.Path]::DirectorySeparatorChar),
        [System.IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd([System.IO.Path]::DirectorySeparatorChar),
        [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs')).TrimEnd([System.IO.Path]::DirectorySeparatorChar),
        [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    )

    if ([string]::IsNullOrWhiteSpace((Split-Path -Leaf $fullPath)) -or $protectedPaths -contains $fullPath) {
        throw "Refusing to use broad or protected install directory '$fullPath'."
    }
}

function Get-RunningPowerMouse {
    @(Get-Process -Name 'PowerMouse' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
}

function Stop-RunningPowerMouse {
    param(
        [Parameter(Mandatory = $true)][string]$CommandExecutable,
        [switch]$AllowForce
    )

    $shutdownProcess = Start-Process -FilePath $CommandExecutable -ArgumentList '--shutdown' -WindowStyle Hidden -Wait -PassThru
    if ($shutdownProcess.ExitCode -ne 0) {
        Write-Warning "The shutdown command exited with code $($shutdownProcess.ExitCode)."
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    do {
        $running = Get-RunningPowerMouse
        if ($running.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)

    if (-not $AllowForce) {
        $processIds = ($running | ForEach-Object { $_.Id }) -join ', '
        throw "PowerMouse is still running (PID $processIds). Close it from the tray and retry, or pass -ForceStop."
    }

    foreach ($process in $running) {
        Stop-Process -Id $process.Id -Force
    }
}

function New-PowerMouseShortcut {
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        try {
            $shortcut.TargetPath = $ExecutablePath
            $shortcut.WorkingDirectory = $WorkingDirectory
            $shortcut.Description = 'Keyboard-first pointer control for Windows'
            $shortcut.IconLocation = "$ExecutablePath,0"
            $shortcut.Save()
        }
        finally {
            if ($null -ne $shortcut) {
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
            }
        }
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}

Assert-SafeInstallDirectory -Path $installFullPath
Assert-SafeInstallDirectory -Path $stagingPath
Assert-SafeInstallDirectory -Path $backupPath

if ($Platform -eq 'Auto') {
    $Platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }
}

if (-not $PSCmdlet.ShouldProcess($installFullPath, "Build, install, and update PowerMouse ($Platform)")) {
    return
}

New-Item -ItemType Directory -Path $installParent -Force | Out-Null
if (Test-Path -LiteralPath $stagingPath) {
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}

if ($UsePublishedBuild) {
    $publishedPath = Join-Path $repoRoot "artifacts\PowerMouse-$Platform"
    foreach ($requiredFile in @('PowerMouse.exe', 'PowerMouse.dll', 'PowerMouse.ClickHints.dll', 'PowerMouse.ClickHints.Windows.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishedPath $requiredFile) -PathType Leaf)) {
            throw "The tested published build is incomplete at '$publishedPath'. Run scripts/build.ps1 first."
        }
    }
    Write-Host "Installing the existing published build from $publishedPath..."
    New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
    Get-ChildItem -LiteralPath $publishedPath -Force | Copy-Item -Destination $stagingPath -Recurse -Force
}
else {
    Write-Host "Building PowerMouse $Platform ($Configuration)..."
    $buildArguments = @{
        Configuration = $Configuration
        Platform = $Platform
        OutputPath = $stagingPath
    }
    if ($SkipTests) {
        $buildArguments.SkipTests = $true
    }

    & (Join-Path $PSScriptRoot 'build.ps1') @buildArguments
    if ($LASTEXITCODE -ne 0) {
        throw "The PowerMouse build failed with exit code $LASTEXITCODE."
    }
}

$stagedExecutable = Join-Path $stagingPath 'PowerMouse.exe'
if (-not (Test-Path -LiteralPath $stagedExecutable -PathType Leaf)) {
    throw "The published executable was not found at '$stagedExecutable'."
}

Stop-RunningPowerMouse -CommandExecutable $stagedExecutable -AllowForce:$ForceStop

$movedExistingInstall = $false
$installedNewBuild = $false
try {
    if (Test-Path -LiteralPath $backupPath) {
        Remove-Item -LiteralPath $backupPath -Recurse -Force
    }

    if (Test-Path -LiteralPath $installFullPath) {
        Move-Item -LiteralPath $installFullPath -Destination $backupPath
        $movedExistingInstall = $true
    }

    Move-Item -LiteralPath $stagingPath -Destination $installFullPath
    $installedNewBuild = $true

    $installedExecutable = Join-Path $installFullPath 'PowerMouse.exe'
    New-PowerMouseShortcut -ExecutablePath $installedExecutable -WorkingDirectory $installFullPath

    if (-not $NoStart) {
        $appProcess = Start-Process -FilePath $installedExecutable -ArgumentList '--show-settings' -PassThru
        Start-Sleep -Seconds 2
        $appProcess.Refresh()
        if ($appProcess.HasExited) {
            throw "The installed PowerMouse process exited during startup with code $($appProcess.ExitCode)."
        }

        Write-Host "PowerMouse is running from $installedExecutable (PID $($appProcess.Id))."
    }
    else {
        Write-Host "PowerMouse is installed at $installedExecutable."
    }
}
catch {
    if ($installedNewBuild -and (Test-Path -LiteralPath $installFullPath)) {
        Remove-Item -LiteralPath $installFullPath -Recurse -Force
    }

    if ($movedExistingInstall -and (Test-Path -LiteralPath $backupPath)) {
        Move-Item -LiteralPath $backupPath -Destination $installFullPath
    }

    throw
}
finally {
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}

Write-Host "Future updates use the same command: .\scripts\install-dev.ps1"
