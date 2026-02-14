#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build script for DistroNexus v2.0.1
.DESCRIPTION
    Builds the .NET application and packages it with the PowerShell module.
    Supports Debug/Release configurations, self-contained publishing, and portable ZIP creation.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.
.PARAMETER Clean
    Clean before building
.PARAMETER Publish
    Create publish output for distribution
.PARAMETER SelfContained
    Create self-contained package with .NET runtime (larger but no prerequisites)
.PARAMETER CreateZip
    Create portable ZIP package after publishing
.PARAMETER Version
    Version string for the build. Default is 2.0.1
.EXAMPLE
    .\build_v2.ps1 -Configuration Release -Publish -CreateZip
.EXAMPLE
    .\build_v2.ps1 -Clean -Publish -SelfContained -CreateZip
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [switch]$Clean,
    
    [switch]$Publish,
    
    [switch]$SelfContained,
    
    [switch]$CreateZip,
    
    [string]$Version = '2.0.1'
)

$ErrorActionPreference = 'Stop'

# Paths
$RootDir = $PSScriptRoot | Split-Path
$SrcDir = Join-Path $RootDir 'src'
$ClientDir = Join-Path $SrcDir 'Client'
$PowerShellDir = Join-Path $SrcDir 'PowerShell'
$ConfigDir = Join-Path $RootDir 'config'
$OutputDir = Join-Path $RootDir 'release'
$PackageName = "DistroNexus-v$Version-$Configuration"
if ($SelfContained) { $PackageName += "-selfcontained" }
$PublishDir = Join-Path $OutputDir $PackageName

Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║           DistroNexus v$Version Build Script                   ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:  $Configuration" -ForegroundColor White
Write-Host "Self-Contained: $SelfContained" -ForegroundColor White
Write-Host "Create ZIP:     $CreateZip" -ForegroundColor White
Write-Host "Root Directory: $RootDir" -ForegroundColor Gray
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
    Write-Host "📦 Publishing application..." -ForegroundColor Yellow
    
    # Create output directory
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }
    New-Item -Path $PublishDir -ItemType Directory -Force | Out-Null
    
    # Publish Desktop app
    Push-Location $ClientDir
    try {
        $publishArgs = @(
            'publish', 'DistroNexus.Desktop',
            '--configuration', $Configuration,
            '--output', $PublishDir,
            '--verbosity', 'minimal'
        )
        
        if ($SelfContained) {
            $publishArgs += '--self-contained', 'true'
            $publishArgs += '-r', 'win-x64'
            $publishArgs += '-p:PublishSingleFile=false'
            $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
        } else {
            $publishArgs += '--self-contained', 'false'
        }
        
        & dotnet @publishArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "✅ Application published successfully" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
    
    # Copy PowerShell module
    Write-Host "📁 Copying PowerShell module..." -ForegroundColor Yellow
    $moduleDestination = Join-Path $PublishDir 'PowerShell'
    
    Copy-Item -Path $PowerShellDir -Destination $moduleDestination -Recurse -Force
    
    # Remove any .git or test files from module
    Get-ChildItem $moduleDestination -Recurse -Include '.git*', '*.Tests.ps1' -Force | Remove-Item -Force -ErrorAction SilentlyContinue
    
    Write-Host "✅ PowerShell module copied" -ForegroundColor Green
    
    # Copy configuration files
    Write-Host "⚙️  Copying configuration files..." -ForegroundColor Yellow
    $configDestination = Join-Path $PublishDir 'config'
    
    if (Test-Path $ConfigDir) {
        Copy-Item -Path $ConfigDir -Destination $configDestination -Recurse -Force
        Write-Host "✅ Configuration files copied" -ForegroundColor Green
    } else {
        # Create default config directory
        New-Item -Path $configDestination -ItemType Directory -Force | Out-Null
        
        # Create default settings.json
        $defaultSettings = @{
            DefaultInstallPath = 'C:\WSL'
            DefaultWslVersion = 2
            DefaultUsername = 'root'
            CatalogUrl = 'https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/main/config/catalog.json'
            Theme = 'Auto'
            EnableLogging = $true
        }
        $defaultSettings | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $configDestination 'settings.json') -Encoding UTF8
        Write-Host "✅ Default configuration created" -ForegroundColor Green
    }
    
    # Copy documentation files
    Write-Host "📄 Copying documentation..." -ForegroundColor Yellow
    $docFiles = @('README.md', 'LICENSE', 'CHANGELOG.md')
    foreach ($doc in $docFiles) {
        $docPath = Join-Path $RootDir $doc
        if (Test-Path $docPath) {
            Copy-Item -Path $docPath -Destination $PublishDir -Force
        }
    }
    Write-Host "✅ Documentation copied" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                    Publish Complete                          ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host "Output: $PublishDir" -ForegroundColor White
    Write-Host ""
    Write-Host "Package contents:" -ForegroundColor White
    Get-ChildItem $PublishDir | ForEach-Object {
        $size = if ($_.PSIsContainer) { "[DIR]" } else { "{0:N2} MB" -f ($_.Length / 1MB) }
        Write-Host "  📁 $($_.Name) $size" -ForegroundColor Gray
    }
}

# Create ZIP if requested
if ($CreateZip -and $Publish) {
    Write-Host ""
    Write-Host "🗜️  Creating portable ZIP package..." -ForegroundColor Yellow
    
    $zipPath = "$PublishDir.zip"
    
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Host "✅ ZIP package created: $zipPath ({0:N2} MB)" -f $zipSize -ForegroundColor Green
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                  Build Script Complete                       ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
