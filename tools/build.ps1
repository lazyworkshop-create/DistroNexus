#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build script for DistroNexus v2.0.1
.DESCRIPTION
    Builds the .NET application and packages it with the PowerShell module.
    Supports Debug/Release configurations, self-contained publishing, portable ZIP creation,
    and Store MSIX/MSIXUpload packaging.
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
.PARAMETER StoreBuild
    Build Microsoft Store package artifacts (.msixbundle and .msixupload)
.EXAMPLE
    .\build.ps1 -Configuration Release -Publish -CreateZip
.EXAMPLE
    .\build.ps1 -Clean -Publish -SelfContained -CreateZip
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [switch]$Clean,
    
    [switch]$Publish,
    
    [switch]$SelfContained,
    
    [switch]$CreateZip,

    [switch]$StoreBuild,
    
    [string]$Version = '2.0.1'
)

$ErrorActionPreference = 'Stop'

# Paths
$RootDir = $PSScriptRoot | Split-Path
$SrcDir = Join-Path $RootDir 'src'
$ClientDir = Join-Path $SrcDir 'Client'
$PowerShellDir = Join-Path $SrcDir 'PowerShell'
$ConfigDir = Join-Path $RootDir 'config'
$StorePackageProject = Join-Path $SrcDir 'DistroNexus.Package\DistroNexus.Package.wapproj'
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
Write-Host "Store Build:    $StoreBuild" -ForegroundColor White
Write-Host "Root Directory: $RootDir" -ForegroundColor Gray
Write-Host ""

function Get-StoreVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputVersion
    )

    if ($InputVersion -notmatch '^(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?$') {
        throw "Invalid version format '$InputVersion'. Use Major.Minor.Patch or Major.Minor.Patch.0 for Store builds."
    }

    $major = $matches[1]
    $minor = $matches[2]
    $patch = $matches[3]
    $revision = if ($matches[4]) { $matches[4] } else { '0' }

    if ($revision -ne '0') {
        throw "Store package version fourth part must be 0. Received '$InputVersion'."
    }

    return "$major.$minor.$patch.0"
}

function Get-DesktopBridgeTargetsPath {
    $candidates = New-Object System.Collections.Generic.List[string]

    $msbuildDesktopBridge = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\Microsoft\DesktopBridge\'
    if (-not [string]::IsNullOrWhiteSpace($msbuildDesktopBridge)) {
        $candidates.Add($msbuildDesktopBridge)
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $installPaths = & $vswhere -all -products * -property installationPath 2>$null
        foreach ($installPath in $installPaths) {
            if (-not [string]::IsNullOrWhiteSpace($installPath)) {
                $candidates.Add((Join-Path $installPath 'MSBuild\Microsoft\DesktopBridge\'))
            }
        }
    }

    $globPatterns = @(
        'Microsoft Visual Studio\*\*\MSBuild\Microsoft\DesktopBridge\',
        'Microsoft Visual Studio\*\*\MSBuild\Current\Bin\..\..\Microsoft\DesktopBridge\'
    )

    foreach ($root in @(${env:ProgramFiles}, ${env:ProgramFiles(x86)})) {
        if ([string]::IsNullOrWhiteSpace($root) -or -not (Test-Path $root)) {
            continue
        }

        foreach ($pattern in $globPatterns) {
            $fullPattern = Join-Path $root $pattern
            $resolved = Resolve-Path -Path $fullPattern -ErrorAction SilentlyContinue
            foreach ($entry in $resolved) {
                if ($entry -and $entry.Path) {
                    $candidates.Add($entry.Path)
                }
            }
        }
    }

    $dedupedCandidates = $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($candidate in $dedupedCandidates) {
        $propsPath = Join-Path $candidate 'Microsoft.DesktopBridge.props'
        if (Test-Path $propsPath) {
            return $candidate
        }
    }

    return $null
}

function Get-VisualStudioMsBuildPath {
    $candidates = New-Object System.Collections.Generic.List[string]

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $installPaths = & $vswhere -all -products * -property installationPath 2>$null
        foreach ($installPath in $installPaths) {
            if (-not [string]::IsNullOrWhiteSpace($installPath)) {
                $candidates.Add((Join-Path $installPath 'MSBuild\Current\Bin\MSBuild.exe'))
            }
        }
    }

    foreach ($root in @(${env:ProgramFiles}, ${env:ProgramFiles(x86)})) {
        if ([string]::IsNullOrWhiteSpace($root) -or -not (Test-Path $root)) {
            continue
        }

        $pattern = Join-Path $root 'Microsoft Visual Studio\*\*\MSBuild\Current\Bin\MSBuild.exe'
        $resolved = Resolve-Path -Path $pattern -ErrorAction SilentlyContinue
        foreach ($entry in $resolved) {
            if ($entry -and $entry.Path) {
                $candidates.Add($entry.Path)
            }
        }
    }

    $deduped = $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
    foreach ($candidate in $deduped) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Get-VisualStudioMsBuildPathForDesktopBridge {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DesktopBridgePath
    )

    $normalized = $DesktopBridgePath.TrimEnd('\', '/')

    $marker = '\MSBuild\Microsoft\DesktopBridge'
    $index = $normalized.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase)
    if ($index -lt 0) {
        return $null
    }

    $installationRoot = $normalized.Substring(0, $index)
    $candidate = Join-Path $installationRoot 'MSBuild\Current\Bin\MSBuild.exe'

    if (Test-Path $candidate) {
        return $candidate
    }

    return $null
}

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
    $zipMessage = "✅ ZIP package created: {0} ({1:N2} MB)" -f $zipPath, $zipSize
    Write-Host $zipMessage -ForegroundColor Green
}

if ($StoreBuild) {
    Write-Host ""
    Write-Host "🛍️  Building Microsoft Store package..." -ForegroundColor Yellow

    if (-not (Test-Path $StorePackageProject)) {
        throw "Store package project not found: $StorePackageProject"
    }

    $storeVersion = Get-StoreVersion -InputVersion $Version
    $desktopBridgePath = Get-DesktopBridgeTargetsPath
    $vsMsBuildPath = $null

    if ([string]::IsNullOrWhiteSpace($desktopBridgePath)) {
        throw "Desktop Bridge targets not found. Install Visual Studio Build Tools with Universal Windows Platform build tools/Desktop Bridge workload."
    }

    $vsMsBuildPath = Get-VisualStudioMsBuildPathForDesktopBridge -DesktopBridgePath $desktopBridgePath
    if ([string]::IsNullOrWhiteSpace($vsMsBuildPath)) {
        $vsMsBuildPath = Get-VisualStudioMsBuildPath
    }

    if ([string]::IsNullOrWhiteSpace($vsMsBuildPath)) {
        throw "Visual Studio MSBuild.exe not found. Install Visual Studio Build Tools and ensure MSBuild is available."
    }

    if (-not $desktopBridgePath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $desktopBridgePath = $desktopBridgePath + [System.IO.Path]::DirectorySeparatorChar
    }

    $storeOutputDir = Join-Path $OutputDir "store\"

    if (Test-Path $storeOutputDir) {
        Remove-Item $storeOutputDir -Recurse -Force
    }

    New-Item -Path $storeOutputDir -ItemType Directory -Force | Out-Null

    $storeBuildArgs = @(
        $StorePackageProject,
        '/restore',
        "/p:Configuration=$Configuration",
        '/p:Platform=x64',
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:AppxBundle=Always',
        '/p:AppxBundlePlatforms=x64|ARM64',
        '/p:UapAppxPackageBuildMode=StoreUpload',
        '/p:AppxPackageSigningEnabled=false',
        "/p:DesktopBridgePath=$desktopBridgePath",
        "/p:PackageVersion=$storeVersion",
        "/p:AppxPackageDir=$storeOutputDir"
    )

    Push-Location $RootDir
    try {
        Write-Host "Using MSBuild: $vsMsBuildPath" -ForegroundColor Gray
        Write-Host "Using DesktopBridge targets: $desktopBridgePath" -ForegroundColor Gray

        & $vsMsBuildPath @storeBuildArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Store build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    $msixBundles = Get-ChildItem -Path $storeOutputDir -Recurse -Filter '*.msixbundle' -ErrorAction SilentlyContinue
    $msixUploads = Get-ChildItem -Path $storeOutputDir -Recurse -Filter '*.msixupload' -ErrorAction SilentlyContinue

    if ($msixBundles.Count -eq 0) {
        throw "Store build completed but no .msixbundle artifact was found under $storeOutputDir"
    }

    if ($msixUploads.Count -eq 0) {
        throw "Store build completed but no .msixupload artifact was found under $storeOutputDir"
    }

    Write-Host "✅ Store package build completed" -ForegroundColor Green
    Write-Host "Store version: $storeVersion" -ForegroundColor White
    Write-Host "Artifacts:" -ForegroundColor White

    foreach ($artifact in (@($msixBundles) + @($msixUploads))) {
        $sizeMb = [math]::Round($artifact.Length / 1MB, 2)
        Write-Host "  📦 $($artifact.FullName) ($sizeMb MB)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                  Build Script Complete                       ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
