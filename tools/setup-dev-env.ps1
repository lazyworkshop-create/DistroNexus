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
    [switch]$AutoInstall,
    [ValidateRange(1, 240)]
    [int]$InstallTimeoutMinutes = 45,
    [ValidateRange(1, 240)]
    [int]$DependencyTimeoutMinutes = 30
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

function Invoke-CommandWithTimeout {
    param (
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $false)][string[]]$Arguments = @(),
        [Parameter(Mandatory = $true)][int]$TimeoutMinutes,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )

    $outputPath = Join-Path ([System.IO.Path]::GetTempPath()) ("distronexus-setup-{0}-{1}.log" -f ([System.IO.Path]::GetFileNameWithoutExtension($FilePath)), [Guid]::NewGuid().ToString("N"))
    $errorPath = Join-Path ([System.IO.Path]::GetTempPath()) ("distronexus-setup-{0}-{1}.err.log" -f ([System.IO.Path]::GetFileNameWithoutExtension($FilePath)), [Guid]::NewGuid().ToString("N"))

    $resolvedCommand = Get-Command $FilePath -ErrorAction SilentlyContinue | Select-Object -First 1
    $resolvedPath = if ($resolvedCommand -and $resolvedCommand.Source) { $resolvedCommand.Source } else { $FilePath }

    $startFilePath = $resolvedPath
    $startArguments = $Arguments

    if ($FilePath -ieq "npm") {
        $startFilePath = "cmd.exe"
        $startArguments = @("/c", "npm") + $Arguments
    }
    elseif ($resolvedCommand -and $resolvedCommand.CommandType -eq 'ExternalScript' -and $resolvedPath -match '\.ps1$') {
        $startFilePath = "pwsh"
        $startArguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $resolvedPath) + $Arguments
    }
    elseif ($resolvedPath -match '\.(cmd|bat)$') {
        $startFilePath = "cmd.exe"
        $startArguments = @("/c", $resolvedPath) + $Arguments
    }

    $process = Start-Process -FilePath $startFilePath -ArgumentList $startArguments -PassThru -NoNewWindow -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath
    $timedOut = $false

    try {
        if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
            $timedOut = $true
            try {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
            catch {
            }
        }

        $stdOut = if (Test-Path $outputPath) { Get-Content -Path $outputPath -Raw -ErrorAction SilentlyContinue } else { "" }
        $stdErr = if (Test-Path $errorPath) { Get-Content -Path $errorPath -Raw -ErrorAction SilentlyContinue } else { "" }

        if (-not [string]::IsNullOrWhiteSpace($stdOut)) {
            Write-Host $stdOut.TrimEnd()
        }

        if (-not [string]::IsNullOrWhiteSpace($stdErr)) {
            Write-WarningMsg "$DisplayName stderr:"
            Write-Host $stdErr.TrimEnd()
        }

        if ($timedOut) {
            Write-ErrorMsg "$DisplayName timed out after $TimeoutMinutes minute(s)."
            return @{
                Success = $false
                TimedOut = $true
                ExitCode = -1
            }
        }

        return @{
            Success = ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010)
            TimedOut = $false
            ExitCode = $process.ExitCode
        }
    }
    finally {
        Remove-Item -Path $outputPath, $errorPath -ErrorAction SilentlyContinue
    }
}

function Get-VsWherePath {
    $candidate = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $candidate) {
        return $candidate
    }

    return $null
}

function Get-VsInstallations {
    $vswherePath = Get-VsWherePath
    if (-not $vswherePath) {
        return @()
    }

    $json = & $vswherePath -all -prerelease -products * -format json 2>$null | Out-String
    if ([string]::IsNullOrWhiteSpace($json)) {
        return @()
    }

    try {
        $instances = $json | ConvertFrom-Json
    }
    catch {
        return @()
    }

    if ($instances -isnot [System.Collections.IEnumerable] -or $instances -is [string]) {
        $instances = @($instances)
    }

    return @(
        $instances |
            Where-Object { $_.installationPath -and $_.isComplete } |
            Sort-Object @{ Expression = { [version]$_.installationVersion }; Descending = $true }
    )
}

function Test-DesktopBridgeTargetsInstalled {
    param (
        [Parameter(Mandatory = $false)]
        [array]$VsInstallations = @()
    )

    $candidatePaths = New-Object System.Collections.Generic.List[string]

    $msbuildDesktopBridge = Join-Path ${env:ProgramFiles(x86)} "MSBuild\Microsoft\DesktopBridge\"
    $candidatePaths.Add($msbuildDesktopBridge)

    foreach ($installation in $VsInstallations) {
        if ($installation.installationPath) {
            $candidatePaths.Add((Join-Path $installation.installationPath "MSBuild\Microsoft\DesktopBridge\"))
        }
    }

    foreach ($candidate in ($candidatePaths | Select-Object -Unique)) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $propsPath = Join-Path $candidate "Microsoft.DesktopBridge.props"
        if (Test-Path $propsPath) {
            return $true
        }
    }

    return $false
}

function Install-VsDesktopBridgeDependencies {
    param (
        [Parameter(Mandatory = $false)]
        [array]$VsInstallations = @()
    )

    $workloads = @(
        "Microsoft.VisualStudio.Workload.UniversalBuildTools",
        "Microsoft.VisualStudio.Workload.Universal",
        "Microsoft.VisualStudio.ComponentGroup.UWP.VC",
        "Microsoft.VisualStudio.Component.DesktopBridge"
    )

    $installerPath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\setup.exe"
    $installViaVsInstallerSucceeded = $false
    if ((Test-Path $installerPath) -and $VsInstallations.Count -gt 0) {
        $latestInstallation = $VsInstallations | Select-Object -First 1
        Write-Host "  Installing Desktop Bridge dependencies into: $($latestInstallation.installationPath)"

        $args = @(
            "modify",
            "--installPath", $latestInstallation.installationPath,
            "--quiet",
            "--wait",
            "--norestart",
            "--nocache",
            "--includeRecommended"
        )

        foreach ($workload in $workloads) {
            $args += @("--add", $workload)
        }

        $installResult = Invoke-CommandWithTimeout -FilePath $installerPath -Arguments $args -TimeoutMinutes $InstallTimeoutMinutes -DisplayName "Visual Studio Installer"

        if ($installResult.Success) {
            Write-Success "Visual Studio Desktop Bridge dependencies installation completed."
            $installViaVsInstallerSucceeded = $true
        }

        if (-not $installViaVsInstallerSucceeded) {
            Write-WarningMsg "Visual Studio installer modify command failed (exit code: $($installResult.ExitCode))."
        }
    }

    Update-ProcessPath
    $postInstallVsInstances = Get-VsInstallations
    if (Test-DesktopBridgeTargetsInstalled -VsInstallations $postInstallVsInstances) {
        return $true
    }

    if (-not (Check-Command "winget")) {
        Write-ErrorMsg "Cannot auto-install Desktop Bridge dependencies because winget is unavailable."
        return $false
    }

    Write-Host "  Installing Visual Studio Build Tools with UWP/Desktop Bridge workloads via winget..."
    $wingetVsArgs = @(
        "install",
        "--id", "Microsoft.VisualStudio.2022.BuildTools",
        "--exact",
        "--silent",
        "--accept-package-agreements",
        "--accept-source-agreements",
        "--override", "--quiet --wait --norestart --nocache --includeRecommended --add Microsoft.VisualStudio.Workload.UniversalBuildTools --add Microsoft.VisualStudio.ComponentGroup.UWP.VC --add Microsoft.VisualStudio.Component.DesktopBridge"
    )
    $wingetVsResult = Invoke-CommandWithTimeout -FilePath "winget" -Arguments $wingetVsArgs -TimeoutMinutes $InstallTimeoutMinutes -DisplayName "winget BuildTools"

    if ($wingetVsResult.Success) {
        Write-Success "Build Tools installation with Desktop Bridge dependencies completed."
        Update-ProcessPath
        $postWingetVsInstances = Get-VsInstallations
        if (Test-DesktopBridgeTargetsInstalled -VsInstallations $postWingetVsInstances) {
            return $true
        }

        Write-WarningMsg "Desktop Bridge targets are still not detected after winget installation attempt."
        return $false
    }

    Write-ErrorMsg "Failed to auto-install Desktop Bridge dependencies (exit code: $($wingetVsResult.ExitCode))."
    return $false
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
    $wingetArgs = @(
        "install",
        "--id", $PackageId,
        "--exact",
        "--silent",
        "--accept-package-agreements",
        "--accept-source-agreements"
    )
    $wingetResult = Invoke-CommandWithTimeout -FilePath "winget" -Arguments $wingetArgs -TimeoutMinutes $InstallTimeoutMinutes -DisplayName "winget $DisplayName"

    if ($wingetResult.Success) {
        Write-Success "$DisplayName installation completed."
        return $true
    }

    $wingetListArgs = @("list", "--id", $PackageId, "--exact", "--accept-source-agreements")
    $wingetListResult = Invoke-CommandWithTimeout -FilePath "winget" -Arguments $wingetListArgs -TimeoutMinutes 5 -DisplayName "winget list $DisplayName"
    if ($wingetListResult.Success) {
        Write-WarningMsg "$DisplayName is already installed (winget install returned non-zero, likely no upgrade available)."
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
Write-Header "Checking Visual Studio Build Dependencies..."
$vsInstallations = Get-VsInstallations

if ($vsInstallations.Count -gt 0) {
    $latestVs = $vsInstallations | Select-Object -First 1
    Write-Success "Visual Studio is installed ($($latestVs.displayName) - $($latestVs.installationVersion))."
} else {
    Write-WarningMsg "Visual Studio installation was not detected."
}

$desktopBridgeAvailable = Test-DesktopBridgeTargetsInstalled -VsInstallations $vsInstallations
if ($desktopBridgeAvailable) {
    Write-Success "Desktop Bridge targets are installed."
} else {
    if ($AutoInstall) {
        Write-WarningMsg "Desktop Bridge targets are missing. Auto-install is enabled."
        $installed = Install-VsDesktopBridgeDependencies -VsInstallations $vsInstallations
        if ($installed) {
            Update-ProcessPath
            $vsInstallations = Get-VsInstallations
            $desktopBridgeAvailable = Test-DesktopBridgeTargetsInstalled -VsInstallations $vsInstallations
            if ($desktopBridgeAvailable) {
                Write-Success "Desktop Bridge targets are installed."
            } else {
                Write-ErrorMsg "Desktop Bridge targets are still unavailable after installation attempt."
                $hasBlockingIssue = $true
            }
        } else {
            $hasBlockingIssue = $true
        }
    } else {
        Write-ErrorMsg "Desktop Bridge targets are missing. Re-run with -AutoInstall or install Visual Studio UWP/Desktop Bridge workload manually."
        $hasBlockingIssue = $true
    }
}

# 4. Initialize Application Environment
Write-Header "Initializing Application Environment..."
$projectRoot = Split-Path -Parent $PSScriptRoot
$slnxPath = Join-Path $projectRoot "src\Client\DistroNexus.slnx"

if (Test-Path $slnxPath) {
    Write-Host "  Restoring .NET dependencies..."
    $restoreResult = Invoke-CommandWithTimeout -FilePath "dotnet" -Arguments @("restore", $slnxPath) -TimeoutMinutes $DependencyTimeoutMinutes -DisplayName "dotnet restore"
    if ($restoreResult.Success) {
        Write-Success ".NET dependencies restored successfully."
    } else {
        Write-ErrorMsg "Failed to restore .NET dependencies (exit code: $($restoreResult.ExitCode))."
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
        $npmResult = Invoke-CommandWithTimeout -FilePath "npm" -Arguments @("install") -TimeoutMinutes $DependencyTimeoutMinutes -DisplayName "npm install"
        if ($npmResult.Success) {
            Write-Success "npm dependencies installed successfully."
        } else {
            Write-ErrorMsg "Failed to install npm dependencies (exit code: $($npmResult.ExitCode))."
        }
    } finally {
        Pop-Location
    }
} else {
    Write-ErrorMsg "Website directory not found at $websiteDir."
}

Write-Header "Environment Setup Complete!"
Write-Host "You can now build the application or run the website." -ForegroundColor Green
