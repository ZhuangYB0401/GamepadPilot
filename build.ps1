<#
.SYNOPSIS
Builds, tests, and optionally publishes GamepadPilot.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$toolsRoot = Join-Path $root '.tools'
$dotnet = Join-Path $toolsRoot 'dotnet\dotnet.exe'

if (-not (Test-Path -LiteralPath $dotnet)) {
    New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
    $installer = Join-Path $toolsRoot 'dotnet-install.ps1'
    Write-Host 'Downloading .NET 10 SDK...'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & $installer -Channel '10.0' -InstallDir (Join-Path $toolsRoot 'dotnet') -NoPath
}

$env:DOTNET_CLI_HOME = Join-Path $toolsRoot 'home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $toolsRoot 'nuget'

Push-Location $root
try {
    & $dotnet restore 'GamepadPilot.slnx' --configfile (Join-Path $root 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }

    & $dotnet build 'GamepadPilot.slnx' --no-restore -c $Configuration -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    if (-not $SkipTests) {
        & $dotnet run --project 'tests\GamepadPilot.SelfTest\GamepadPilot.SelfTest.csproj' --no-build --no-restore -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Self tests failed.' }
    }

    if ($Publish) {
        $output = Join-Path $root 'artifacts\publish'
        New-Item -ItemType Directory -Force -Path $output | Out-Null
        & $dotnet publish 'src\GamepadPilot.App\GamepadPilot.App.csproj' --no-restore -c $Configuration -r win-x64 --self-contained true -o $output
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $output 'README.md') -Force
        Write-Host "Portable build: $output\GamepadPilot.exe"
    }
}
finally {
    Pop-Location
}
