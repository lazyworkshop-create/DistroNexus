#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Creates a portable ZIP distribution of DistroNexus
.DESCRIPTION
    Convenience script that builds and packages DistroNexus as a portable ZIP.
    Creates both framework-dependent and self-contained packages.
.PARAMETER Version
    Version number for the package. Default is 2.0.0
#>
param(
    [string]$Version = '2.0.0'
)

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot

Write-Host "Creating DistroNexus Portable Packages..." -ForegroundColor Cyan
Write-Host ""

# Create framework-dependent package (smaller, requires .NET runtime)
Write-Host "Building framework-dependent package..." -ForegroundColor Yellow
& "$ScriptDir\build.ps1" -Configuration Release -Clean -Publish -CreateZip -Version $Version

Write-Host ""

# Create self-contained package (larger, no prerequisites)
Write-Host "Building self-contained package..." -ForegroundColor Yellow
& "$ScriptDir\build.ps1" -Configuration Release -Publish -SelfContained -CreateZip -Version $Version

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║               All Packages Created Successfully              ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

$releaseDir = Join-Path (Split-Path $ScriptDir) 'release'
Write-Host "Packages available in: $releaseDir" -ForegroundColor White
Get-ChildItem "$releaseDir\*.zip" | ForEach-Object {
    $size = "{0:N2} MB" -f ($_.Length / 1MB)
    Write-Host "  📦 $($_.Name) - $size" -ForegroundColor Cyan
}
