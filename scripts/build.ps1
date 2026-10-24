[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',

    [string]$OutputPath,

    [switch]$SkipTests,

    [switch]$SkipInteractiveTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "artifacts\PowerMouse-$Platform"
}
else {
    if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath = Join-Path $repoRoot $OutputPath
    }

    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
}

Push-Location $repoRoot
try {
    if (-not $SkipTests) {
        dotnet restore tests\PowerMouse.Core.Tests\PowerMouse.Core.Tests.csproj
        if ($LASTEXITCODE -ne 0) { throw "Test restore failed with exit code $LASTEXITCODE." }

        dotnet test tests\PowerMouse.Core.Tests\PowerMouse.Core.Tests.csproj --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }

        dotnet restore tests\PowerMouse.ClickHints.Tests\PowerMouse.ClickHints.Tests.csproj
        if ($LASTEXITCODE -ne 0) { throw "Click Hints test restore failed with exit code $LASTEXITCODE." }

        dotnet test tests\PowerMouse.ClickHints.Tests\PowerMouse.ClickHints.Tests.csproj --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Click Hints tests failed with exit code $LASTEXITCODE." }

        $windowsTestArguments = @('test', 'tests\PowerMouse.Windows.Tests\PowerMouse.Windows.Tests.csproj', '--configuration', $Configuration)
        if ($SkipInteractiveTests) { $windowsTestArguments += @('--filter', 'TestCategory!=InteractiveDesktop') }
        & dotnet @windowsTestArguments
        if ($LASTEXITCODE -ne 0) { throw "Windows overlay tests failed with exit code $LASTEXITCODE." }
    }

    dotnet restore src\PowerMouse.App\PowerMouse.App.csproj -p:Platform=$Platform
    if ($LASTEXITCODE -ne 0) { throw "Application restore failed with exit code $LASTEXITCODE." }

    dotnet publish src\PowerMouse.App\PowerMouse.App.csproj `
        --configuration $Configuration `
        -p:Platform=$Platform `
        --no-restore `
        --output $outputPath
    if ($LASTEXITCODE -ne 0) { throw "Application publish failed with exit code $LASTEXITCODE." }

    Copy-Item (Join-Path $repoRoot 'LICENSE') -Destination $outputPath -Force
    Copy-Item (Join-Path $repoRoot 'README.md') -Destination $outputPath -Force
    Copy-Item (Join-Path $repoRoot 'CHANGELOG.md') -Destination $outputPath -Force
}
finally {
    Pop-Location
}

Write-Host "PowerMouse was published to $OutputPath"
