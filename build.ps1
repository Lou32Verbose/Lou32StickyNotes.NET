<#
.SYNOPSIS
    Builds the Sticky Notes Classic application for specified platforms.

.DESCRIPTION
    This script builds the Sticky Notes Classic .NET 9.0 application for one or more target platforms.
    It supports building self-contained executables, single-file executables, or framework-dependent builds.
    
    The build output is placed in the 'publish' directory at the root of the repository, with subdirectories
    for each platform/architecture combination.

.PARAMETER Architecture
    The target architecture to build for. Valid options are:
    - win-x64: Windows 64-bit (Intel/AMD)
    - win-x86: Windows 32-bit
    - win-arm64: Windows ARM64
    - linux-x64: Linux 64-bit
    - linux-arm64: Linux ARM64
    - osx-x64: macOS Intel 64-bit
    - osx-arm64: macOS Apple Silicon (M1/M2/M3)
    - all: Build for all supported platforms
    
    Default: win-x64

.PARAMETER SelfContained
    If specified, creates a self-contained executable that includes the .NET runtime.
    This makes the application portable but increases the file size.
    
    Default: Framework-dependent (requires .NET 9.0 runtime to be installed)

.PARAMETER SingleFile
    If specified, publishes the application as a single executable file.
    This combines all dependencies into one file for easier distribution.

.PARAMETER Configuration
    The build configuration to use. Valid options are: Debug, Release
    
    Default: Release

.PARAMETER Clean
    If specified, cleans the publish directory before building.

.EXAMPLE
    .\build.ps1
    Builds the application for Windows x64 as a framework-dependent build.

.EXAMPLE
    .\build.ps1 -Architecture win-x64 -SelfContained -SingleFile
    Builds a self-contained single-file executable for Windows x64.

.EXAMPLE
    .\build.ps1 -Architecture all -SelfContained
    Builds self-contained executables for all supported platforms.

.EXAMPLE
    .\build.ps1 -Architecture linux-x64 -Configuration Debug
    Builds a debug version for Linux x64.

.EXAMPLE
    .\build.ps1 -Architecture osx-arm64 -SelfContained -SingleFile -Clean
    Cleans the publish directory and builds a self-contained single-file executable for macOS ARM64.

.NOTES
    File Name  : build.ps1
    Author     : Lou32 
    Requires   : .NET 9.0 SDK
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64', 'all')]
    [string]$Architecture = 'win-x64',
    
    [Parameter()]
    [switch]$SelfContained,
    
    [Parameter()]
    [switch]$SingleFile,
    
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [switch]$Clean
)

# Script configuration
$ErrorActionPreference = 'Stop'
$ProjectPath = Join-Path $PSScriptRoot 'src\StickyNotesClassic.App\StickyNotesClassic.App.csproj'
$PublishRoot = Join-Path $PSScriptRoot 'publish'

# All supported architectures
$AllArchitectures = @('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')

# Determine which architectures to build
if ($Architecture -eq 'all') {
    $TargetArchitectures = $AllArchitectures
    Write-Host "Building for all platforms: $($AllArchitectures -join ', ')" -ForegroundColor Cyan
}
else {
    $TargetArchitectures = @($Architecture)
    Write-Host "Building for: $Architecture" -ForegroundColor Cyan
}

# Display build configuration
Write-Host "`nBuild Configuration:" -ForegroundColor Yellow
Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
Write-Host "  Self-Contained: $SelfContained" -ForegroundColor Gray
Write-Host "  Single File: $SingleFile" -ForegroundColor Gray
Write-Host ""

# Clean publish directory if requested
if ($Clean -and (Test-Path $PublishRoot)) {
    Write-Host "Cleaning publish directory..." -ForegroundColor Yellow
    Remove-Item $PublishRoot -Recurse -Force
    Write-Host "  Cleaned: $PublishRoot" -ForegroundColor Green
}

# Create publish directory if it doesn't exist
if (-not (Test-Path $PublishRoot)) {
    New-Item -ItemType Directory -Path $PublishRoot | Out-Null
}

# Verify project file exists
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

# Build for each architecture
$SuccessCount = 0
$FailureCount = 0
$BuildResults = @()

foreach ($arch in $TargetArchitectures) {
    $OutputPath = Join-Path $PublishRoot $arch
    
    Write-Host "Building for $arch..." -ForegroundColor Cyan
    
    # Prepare dotnet publish arguments
    $publishArgs = @(
        'publish',
        $ProjectPath,
        '-c', $Configuration,
        '-r', $arch,
        '-o', $OutputPath,
        '--nologo'
    )
    
    # Add self-contained flag
    if ($SelfContained) {
        $publishArgs += '--self-contained', 'true'
    }
    else {
        $publishArgs += '--self-contained', 'false'
    }
    
    # Add single file flag
    if ($SingleFile) {
        $publishArgs += '-p:PublishSingleFile=true'
        $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
    }
    
    # Execute the build
    try {
        $output = & dotnet @publishArgs 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $SuccessCount++
            $Status = "SUCCESS"
            $StatusColor = "Green"
            
            # Get output size
            if (Test-Path $OutputPath) {
                $size = (Get-ChildItem $OutputPath -Recurse | Measure-Object -Property Length -Sum).Sum
                $sizeMB = [math]::Round($size / 1MB, 2)
                Write-Host "  Output: $OutputPath ($sizeMB MB)" -ForegroundColor Green
            }
        }
        else {
            $FailureCount++
            $Status = "FAILED"
            $StatusColor = "Red"
            Write-Host "  Error: Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
            Write-Host "  Output: $output" -ForegroundColor Red
        }
    }
    catch {
        $FailureCount++
        $Status = "FAILED"
        $StatusColor = "Red"
        Write-Host "  Exception: $_" -ForegroundColor Red
    }
    
    $BuildResults += [PSCustomObject]@{
        Architecture = $arch
        Status       = $Status
        OutputPath   = $OutputPath
    }
    
    Write-Host "  Status: " -NoNewline
    Write-Host $Status -ForegroundColor $StatusColor
    Write-Host ""
}

# Summary
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Total Builds: $($TargetArchitectures.Count)" -ForegroundColor White
Write-Host "Successful:   " -NoNewline -ForegroundColor White
Write-Host $SuccessCount -ForegroundColor Green
Write-Host "Failed:       " -NoNewline -ForegroundColor White
Write-Host $FailureCount -ForegroundColor $(if ($FailureCount -gt 0) { 'Red' } else { 'Green' })
Write-Host ""

# Display results table
if ($BuildResults.Count -gt 0) {
    Write-Host "Build Results:" -ForegroundColor Yellow
    $BuildResults | Format-Table -AutoSize
}

# Exit with appropriate code
if ($FailureCount -gt 0) {
    exit 1
}
else {
    Write-Host "All builds completed successfully!" -ForegroundColor Green
    Write-Host "Published to: $PublishRoot" -ForegroundColor Cyan
    exit 0
}
