#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build script for DistroNexus v2.0.0
.DESCRIPTION
    Builds the .NET application and packages it with the PowerShell module
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.
.PARAMETER Clean
    Clean before building
.PARAMETER Publish
    Create publish output for distribution
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [switch]$Clean,
    
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'

# Paths
$RootDir = $PSScriptRoot | Split-Path
$SrcDir = Join-Path $RootDir 'src'
$ClientDir = Join-Path $SrcDir 'Client'
$PowerShellDir = Join-Path $SrcDir 'PowerShell'
$OutputDir = Join-Path $RootDir 'release'
$PublishDir = Join-Path $OutputDir "DistroNexus-$Configuration"

Write-Host "=== DistroNexus v2.0.0 Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Root Directory: $RootDir"
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    
    Push-Location $ClientDir
    try {
        dotnet clean --configuration $Configuration --verbosity quiet
    }
    finally {
        Pop-Location
    }
    
    Write-Host "Clean complete." -ForegroundColor Green
    Write-Host ""
}

# Build .NET solution
Write-Host "Building .NET solution..." -ForegroundColor Yellow
Push-Location $ClientDir
try {
    dotnet build --configuration $Configuration --verbosity minimal
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "Build complete." -ForegroundColor Green
}
finally {
    Pop-Location
}
Write-Host ""

# Publish if requested
if ($Publish) {
    Write-Host "Publishing application..." -ForegroundColor Yellow
    
    # Create output directory
    if (-not (Test-Path $PublishDir)) {
        New-Item -Path $PublishDir -ItemType Directory -Force | Out-Null
    }
    
    # Publish Desktop app
    Push-Location $ClientDir
    try {
        $publishOutput = Join-Path $PublishDir 'app'
        
        dotnet publish DistroNexus.Desktop `
            --configuration $Configuration `
            --output $publishOutput `
            --self-contained false `
            --verbosity minimal
        
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "Application published to: $publishOutput" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
    
    # Copy PowerShell module
    Write-Host "Copying PowerShell module..." -ForegroundColor Yellow
    $moduleDestination = Join-Path $publishOutput 'PowerShell'
    
    if (Test-Path $moduleDestination) {
        Remove-Item $moduleDestination -Recurse -Force
    }
    
    Copy-Item -Path $PowerShellDir -Destination $moduleDestination -Recurse -Force
    Write-Host "PowerShell module copied." -ForegroundColor Green
    
    # Copy configuration files
    Write-Host "Copying configuration files..." -ForegroundColor Yellow
    $configSource = Join-Path $RootDir 'config'
    $configDestination = Join-Path $publishOutput 'config'
    
    if (Test-Path $configDestination) {
        Remove-Item $configDestination -Recurse -Force
    }
    
    Copy-Item -Path $configSource -Destination $configDestination -Recurse -Force
    Write-Host "Configuration files copied." -ForegroundColor Green
    
    # Copy README and LICENSE
    Copy-Item -Path (Join-Path $RootDir 'README.md') -Destination $publishOutput -Force
    Copy-Item -Path (Join-Path $RootDir 'LICENSE') -Destination $publishOutput -Force
    
    Write-Host ""
    Write-Host "=== Publish Complete ===" -ForegroundColor Cyan
    Write-Host "Output directory: $PublishDir" -ForegroundColor White
    Write-Host ""
    Write-Host "Package contents:" -ForegroundColor White
    Get-ChildItem $publishOutput | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== Build Script Complete ===" -ForegroundColor Cyan
