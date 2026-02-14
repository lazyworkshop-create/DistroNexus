#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the DistroNexus Windows installer using Inno Setup
.DESCRIPTION
    Creates a Windows installer (.exe) for DistroNexus using Inno Setup.
    Requires Inno Setup 6.0 or later to be installed.
.PARAMETER Version
    Version number for the installer. Default is 2.0.1
.PARAMETER IssPath
    Path to the Inno Setup compiler (iscc.exe). Auto-detected if not specified.
#>
param(
    [string]$Version = '2.0.1',
    [string]$IssPath
)

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot
$RootDir = Split-Path $ScriptDir

Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║          DistroNexus Installer Build Script                  ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Find Inno Setup compiler
if (-not $IssPath) {
    $possiblePaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $IssPath = $path
            break
        }
    }
}

if (-not $IssPath -or -not (Test-Path $IssPath)) {
    Write-Host "❌ Inno Setup compiler (ISCC.exe) not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Inno Setup from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Or specify the path using -IssPath parameter" -ForegroundColor Yellow
    exit 1
}

Write-Host "Using Inno Setup: $IssPath" -ForegroundColor Gray
Write-Host ""

# First, build and publish the application
Write-Host "📦 Building application for installer..." -ForegroundColor Yellow
& "$ScriptDir\build.ps1" -Configuration Release -Publish -Version $Version

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Ensure installer output directory exists
$installerOutputDir = Join-Path $RootDir 'release\installer'
if (-not (Test-Path $installerOutputDir)) {
    New-Item -Path $installerOutputDir -ItemType Directory -Force | Out-Null
}

# Build the installer
Write-Host "🔧 Building installer with Inno Setup..." -ForegroundColor Yellow
$issFile = Join-Path $ScriptDir 'installer.iss'

& $IssPath /DMyAppVersion=$Version $issFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Installer build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║               Installer Built Successfully                    ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

$installerFile = Join-Path $installerOutputDir "DistroNexus-$Version-Setup.exe"
if (Test-Path $installerFile) {
    $size = "{0:N2} MB" -f ((Get-Item $installerFile).Length / 1MB)
    Write-Host "Installer: $installerFile ($size)" -ForegroundColor Cyan
} else {
    Write-Host "Installer created in: $installerOutputDir" -ForegroundColor Cyan
}
