[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$InstallPath)

$ErrorActionPreference = 'Stop'
try {
    $executable = [IO.Path]::GetFullPath((Join-Path $InstallPath 'PowerMouse.exe'))
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $taskName = "PowerMouse Startup $sid"
    $scheduler = New-Object -ComObject 'Schedule.Service'
    try {
        $scheduler.Connect()
        $folder = $scheduler.GetFolder('\')
        try {
            $task = $null
            try { $task = $folder.GetTask($taskName) }
            catch {
                if ($_.Exception.HResult -ne -2147024894 -and
                    $_.Exception.InnerException.HResult -ne -2147024894) { throw }
            }
            if ($null -ne $task) {
                try {
                    $taskXml = [xml]$task.Xml
                    $command = [string]$taskXml.Task.Actions.Exec.Command
                    $ownsTask = $command -and [IO.Path]::GetFullPath($command) -ieq $executable
                }
                finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($task) }
                if ($ownsTask) { $folder.DeleteTask($taskName, 0) }
            }
        }
        finally { if ($null -ne $folder) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($folder) } }
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($scheduler) }

    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $entry = Get-ItemPropertyValue -LiteralPath $runKey -Name PowerMouse -ErrorAction SilentlyContinue
    if ($entry -ieq ('"' + $executable + '"') -or $entry -ieq $executable) {
        Remove-ItemProperty -LiteralPath $runKey -Name PowerMouse
    }
    # Settings/logs and startup entries pointing to another installation are preserved.
    exit 0
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
