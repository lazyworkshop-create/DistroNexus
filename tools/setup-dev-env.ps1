<#
.SYNOPSIS
    Checks and initializes the development environment for DistroNexus.

.DESCRIPTION
    This script verifies that all required tools (.NET SDKs, Node.js, Visual Studio) are installed.
    It then restores .NET dependencies for the application and installs npm packages for the website.

.EXAMPLE
    .\setup-dev-env.ps1

.EXAMPLE
    .\setup-dev-env.ps1 -AutoInstall
#>

[CmdletBinding()]
param (
    [switch]$AutoInstall
)

$ErrorActionPreference = "Stop"

function Write-Header {
    param ([string]$Text)
    Write-Host "`n==> $Text" -ForegroundColor Cyan
}

function Write-Success {
    param ([string]$Text)
    Write-Host "  [OK] $Text" -ForegroundColor Green
}

function Write-WarningMsg {
    param ([string]$Text)
    Write-Host "  [WARN] $Text" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    param ([string]$Text)
    Write-Host "  [ERROR] $Text" -ForegroundColor Red
}

function Check-Command {
    param ([string]$Command)
    $cmd = Get-Command $Command -ErrorAction SilentlyContinue
    if ($cmd) {
        return $true
    }
    return $false
}

function Update-ProcessPath {
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = "$machinePath;$userPath"
}

function Install-WithWinget {
    param (
        [Parameter(Mandatory = $true)][string]$PackageId,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )

    if (-not (Check-Command "winget")) {
        Write-ErrorMsg "winget is not available. Install '$DisplayName' manually."
        return $false
    }

    Write-Host "  Installing $DisplayName via winget..."
    winget install --id $PackageId --exact --silent --accept-package-agreements --accept-source-agreements

    if ($LASTEXITCODE -eq 0) {
        Write-Success "$DisplayName installation completed."
        return $true
    }

    Write-ErrorMsg "Failed to install $DisplayName via winget."
    return $false
}

Write-Header "Checking Prerequisites..."
$hasBlockingIssue = $false

# 1. Check .NET SDKs
$dotnetInstalled = Check-Command "dotnet"
if (-not $dotnetInstalled) {
    if ($AutoInstall) {
        Write-WarningMsg ".NET SDK not found. Auto-install is enabled."
        $installed = Install-WithWinget -PackageId "Microsoft.DotNet.SDK.10" -DisplayName ".NET 10 SDK"
        if (-not $installed) {
            $hasBlockingIssue = $true
        }
    } else {
        Write-ErrorMsg ".NET SDK is not installed. Re-run with -AutoInstall or install .NET 10 SDK manually."
        $hasBlockingIssue = $true
    }
}

$dotnetInstalled = Check-Command "dotnet"
$sdks = @()
if ($dotnetInstalled) {
    $sdks = dotnet --list-sdks
}

$requiredSdks = @("10.0")
$missingSdks = @()

foreach ($sdk in $requiredSdks) {
    if ($dotnetInstalled -and $sdks -match "^$sdk") {
        Write-Success ".NET $sdk SDK is installed."
    } else {
        $missingSdks += $sdk
    }
}

if ($missingSdks.Count -gt 0) {
    if ($AutoInstall) {
        Write-WarningMsg "Missing .NET SDKs: $($missingSdks -join ', '). Attempting to install .NET 10 SDK..."
        $installed = Install-WithWinget -PackageId "Microsoft.DotNet.SDK.10" -DisplayName ".NET 10 SDK"
        if (-not $installed) {
            $hasBlockingIssue = $true
        }
    } else {
        Write-ErrorMsg "Missing .NET SDKs: $($missingSdks -join ', '). Re-run with -AutoInstall or install manually."
        $hasBlockingIssue = $true
    }
}

# 2. Check Node.js and npm
$nodeInstalled = Check-Command "node"
if (-not $nodeInstalled) {
    if ($AutoInstall) {
        Write-WarningMsg "Node.js not found. Auto-install is enabled."
        Install-WithWinget -PackageId "OpenJS.NodeJS.LTS" -DisplayName "Node.js LTS" | Out-Null
        Update-ProcessPath

        $nodeInstalled = Check-Command "node"
        if (-not $nodeInstalled) {
            Write-ErrorMsg "Node.js is still unavailable after installation attempt."
            $hasBlockingIssue = $true
        }
    } else {
        Write-ErrorMsg "Node.js is not installed. Re-run with -AutoInstall or install Node.js LTS manually."
        $hasBlockingIssue = $true
    }
}

$nodeInstalled = Check-Command "node"
if ($nodeInstalled) {
    $nodeVersion = node --version
    Write-Success "Node.js is installed ($nodeVersion)."
}

$npmInstalled = Check-Command "npm"
if ($npmInstalled) {
    $npmVersion = npm --version
    Write-Success "npm is installed (v$npmVersion)."
} else {
    if ($AutoInstall -and -not $nodeInstalled) {
        Write-WarningMsg "npm was not found because Node.js is unavailable."
        $hasBlockingIssue = $true
    } elseif ($AutoInstall) {
        Write-WarningMsg "npm is not found. Reinstalling Node.js LTS to restore npm..."
        Install-WithWinget -PackageId "OpenJS.NodeJS.LTS" -DisplayName "Node.js LTS" | Out-Null
        Update-ProcessPath
        $npmInstalled = Check-Command "npm"
        if ($npmInstalled) {
            $npmVersion = npm --version
            Write-Success "npm is installed (v$npmVersion)."
        } else {
            Write-ErrorMsg "npm is still unavailable after Node.js installation."
            $hasBlockingIssue = $true
        }
    } else {
        Write-ErrorMsg "npm is not installed. Re-run with -AutoInstall or reinstall Node.js LTS manually."
        $hasBlockingIssue = $true
    }
}

if ($hasBlockingIssue) {
    Write-ErrorMsg "Prerequisite check failed. Resolve missing tools, then run this script again."
    exit 1
}

# 3. Check Visual Studio
$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswherePath) {
    $vsInstances = & $vswherePath -prerelease -products * -requires Microsoft.Component.MSBuild -format json | ConvertFrom-Json
    if ($vsInstances) {
        $latestVs = $vsInstances | Sort-Object -Property installationVersion -Descending | Select-Object -First 1
        Write-Success "Visual Studio is installed ($($latestVs.displayName) - $($latestVs.installationVersion))."
    } else {
        Write-WarningMsg "Visual Studio with MSBuild not found. You may need it for packaging."
    }
} else {
    Write-WarningMsg "vswhere.exe not found. Cannot verify Visual Studio installation."
}

# 4. Initialize Application Environment
Write-Header "Initializing Application Environment..."
$projectRoot = Split-Path -Parent $PSScriptRoot
$slnxPath = Join-Path $projectRoot "src\Client\DistroNexus.slnx"

if (Test-Path $slnxPath) {
    Write-Host "  Restoring .NET dependencies..."
    dotnet restore $slnxPath
    if ($LASTEXITCODE -eq 0) {
        Write-Success ".NET dependencies restored successfully."
    } else {
        Write-ErrorMsg "Failed to restore .NET dependencies."
    }
} else {
    Write-ErrorMsg "Solution file not found at $slnxPath."
}

# 5. Initialize Website Environment
Write-Header "Initializing Website Environment..."
$websiteDir = Join-Path $projectRoot "website"

if (Test-Path $websiteDir) {
    Write-Host "  Installing npm dependencies..."
    Push-Location $websiteDir
    try {
        npm install
        if ($LASTEXITCODE -eq 0) {
            Write-Success "npm dependencies installed successfully."
        } else {
            Write-ErrorMsg "Failed to install npm dependencies."
        }
    } finally {
        Pop-Location
    }
} else {
    Write-ErrorMsg "Website directory not found at $websiteDir."
}

Write-Header "Environment Setup Complete!"
Write-Host "You can now build the application or run the website." -ForegroundColor Green
