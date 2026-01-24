# Windows Release Build & Package Script (PowerShell)
# Usage: .\tools\windows_release.ps1 [Version]

param (
    [string]$Version = "1.0.2"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\.."
$SrcDir = Join-Path $ProjectRoot "src"
$OutputDir = Join-Path $ProjectRoot "build"
$ReleaseDir = Join-Path $ProjectRoot "release"
$PackageDir = Join-Path $ProjectRoot "tools\packaging"

Write-Host "=== DistroNexus Windows Release Tool ==="
Write-Host "Project Root: $ProjectRoot"
Write-Host "Version: $Version"

# --- 1. CLEAN & BUILD ---
Write-Host "`n[1/4] Building Project..."

# Ensure Output Directory
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Environment Setup
$env:GOFLAGS = "-ldflags=-s -w"
$env:CGO_ENABLED = "1"
$env:CC = "gcc" # MinGW gcc must be in PATH

# Check GCC
if (-not (Get-Command gcc -ErrorAction SilentlyContinue)) {
    Write-Error "GCC (MinGW) not found in PATH. Please install MinGW-w64."
    exit 1
}

# Build using Fyne or Go
Push-Location $SrcDir
    Write-Host "Tidying modules..."
    go mod tidy

    if (Get-Command fyne -ErrorAction SilentlyContinue) {
        Write-Host "Using Fyne CLI for packaging (with icon)..."
        # Use absolute path for icon to avoid resolution issues
        $IconPath = Join-Path $ProjectRoot "tools\icon.png"
        
        # Run Fyne Package
        # Note: fyne package output path depends on execution, trying to force it
        # We use strict quoting for the path
        & fyne package -os windows -icon "$IconPath" -name DistroNexus --src ./cmd/gui
        
        if (Test-Path "DistroNexus.exe") {
            Move-Item "DistroNexus.exe" "$OutputDir\DistroNexus.exe" -Force
        } elseif (Test-Path "cmd\gui\DistroNexus.exe") {
             Move-Item "cmd\gui\DistroNexus.exe" "$OutputDir\DistroNexus.exe" -Force
        } else {
            Write-Error "Fyne build failed: Output executable not found."
            exit 1
        }
    } else {
        Write-Host "Fyne CLI not found, falling back to standard go build..."
        go build -ldflags "-s -w -H=windowsgui" -o "$OutputDir\DistroNexus.exe" ./cmd/gui/main.go
    }
Pop-Location

# Copy Resources to Build Dir
Write-Host "Copying resources to build directory..."
Copy-Item -Recurse "$ProjectRoot\config" "$OutputDir\"
Copy-Item -Recurse "$ProjectRoot\scripts" "$OutputDir\"

# --- 2. CREATE ZIP ARCHIVE ---
Write-Host "`n[2/4] Creating Portable ZIP..."

# Ensure Release Directory
if (-not (Test-Path $ReleaseDir)) { New-Item -ItemType Directory -Path $ReleaseDir | Out-Null }

$ZipName = "DistroNexus_v${Version}_portable.zip"
$TmpZipRoot = Join-Path $ReleaseDir "tmp_zip"
$TmpZipDir = Join-Path $TmpZipRoot "DistroNexus"

# Prepare Temp Directory
if (Test-Path $TmpZipRoot) { Remove-Item $TmpZipRoot -Recurse -Force }
New-Item -ItemType Directory -Path $TmpZipDir | Out-Null

# Copy Artifacts
Copy-Item "$OutputDir\DistroNexus.exe" "$TmpZipDir\"
Copy-Item -Recurse "$ProjectRoot\scripts" "$TmpZipDir\"
Copy-Item -Recurse "$ProjectRoot\config" "$TmpZipDir\"
Copy-Item "$ProjectRoot\README.md" "$TmpZipDir\"
if (Test-Path "$ProjectRoot\README_CN.md") { Copy-Item "$ProjectRoot\README_CN.md" "$TmpZipDir\" }
if (Test-Path "$ProjectRoot\LICENSE") { Copy-Item "$ProjectRoot\LICENSE" "$TmpZipDir\" }

# Release Notes
$RelNoteSrc = "$ProjectRoot\docs\release_notes\v$Version.md"
if (Test-Path $RelNoteSrc) {
    Copy-Item $RelNoteSrc "$TmpZipDir\RELEASE_NOTES.md"
}

# Cleanup Dev Artifacts
if (Test-Path "$TmpZipDir\scripts\logs") { Remove-Item "$TmpZipDir\scripts\logs" -Recurse -Force }
if (Test-Path "$TmpZipDir\config\instances.json") { Remove-Item "$TmpZipDir\config\instances.json" -Force }

# Create Zip
$ZipPath = Join-Path $ReleaseDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

# Use Compress-Archive
Compress-Archive -Path "$TmpZipDir" -DestinationPath $ZipPath
Write-Host "Portable ZIP created: $ZipPath"

# Cleanup Temp
Remove-Item $TmpZipRoot -Recurse -Force

# --- 3. CREATE INSTALLER (INNO SETUP) ---
Write-Host "`n[3/4] Building Installer..."

if (Get-Command iscc -ErrorAction SilentlyContinue) {
    Push-Location $PackageDir
        # Run ISCC
        Write-Host "Running Inno Setup Compiler..."
        # Note: /dMyAppVersion is standard command line def for ISCC
        & iscc "/dMyAppVersion=$Version" "DistroNexus.iss"
    Pop-Location
    Write-Host "Installer generation complete."
} else {
    Write-Warning "Inno Setup Compiler (iscc) not found. Skipping installer generation."
}

Write-Host "`n[4/4] Packaging Complete."
