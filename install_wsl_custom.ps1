# PowerShell script to interactively download and create a custom WSL2 instance
# Author: GitHub Copilot

param (
    [string]$DistroName,
    [string]$InstallPath,
    [string]$DownloadUrl
)

$ErrorActionPreference = "Stop"

# --- Distro Definitions ---
$DistroCatalog = [ordered]@{
    "1" = @{
        Name = "Ubuntu"
        Versions = [ordered]@{
            "1" = @{ Name = "Ubuntu 24.04 LTS"; Url = "https://wslstorestorage.blob.core.windows.net/wslblob/Ubuntu2404-240425.AppxBundle"; DefaultName = "Ubuntu-24.04"; Filename = "Ubuntu-24.04.AppxBundle" }
            "2" = @{ Name = "Ubuntu 22.04 LTS"; Url = "https://aka.ms/wslubuntu2204"; DefaultName = "Ubuntu-22.04"; Filename = "Ubuntu-22.04.AppxBundle" }
            "3" = @{ Name = "Ubuntu 20.04 LTS"; Url = "https://aka.ms/wslubuntu2004"; DefaultName = "Ubuntu-20.04"; Filename = "Ubuntu-20.04.AppxBundle" }
            "4" = @{ Name = "Ubuntu 18.04 LTS"; Url = "https://aka.ms/wsl-ubuntu-1804"; DefaultName = "Ubuntu-18.04"; Filename = "Ubuntu-18.04.AppxBundle" }
        }
    }
    "2" = @{
        Name = "Debian"
        Versions = [ordered]@{
            "1" = @{ Name = "Debian GNU/Linux"; Url = "https://aka.ms/wsl-debian-gnulinux"; DefaultName = "Debian"; Filename = "Debian.appx" }
        }
    }
    "3" = @{
        Name = "Kali Linux"
        Versions = [ordered]@{
            "1" = @{ Name = "Kali Linux Rolling"; Url = "https://aka.ms/wsl-kali-linux-new"; DefaultName = "Kali-Rolling"; Filename = "Kali.appx" }
        }
    }
    "4" = @{
        Name = "Oracle Linux"
        Versions = [ordered]@{
            "1" = @{ Name = "Oracle Linux 9.1"; Url = "https://publicwsldistros.blob.core.windows.net/wsldistrostorage/OracleLinux_9.1-230428.Appx"; DefaultName = "OracleLinux-9.1"; Filename = "OracleLinux-9.1.appx" }
            "2" = @{ Name = "Oracle Linux 8.7"; Url = "https://publicwsldistros.blob.core.windows.net/wsldistrostorage/OracleLinux_8.7-230428.Appx"; DefaultName = "OracleLinux-8.7"; Filename = "OracleLinux-8.7.appx" }
        }
    }
}

function Show-Menu {
    param ($Title, $Options)
    Write-Host "`n=== $Title ===" -ForegroundColor Cyan
    foreach ($key in $Options.Keys) {
        Write-Host "[$key] $($Options[$key].Name)"
    }
}

# --- Interactive Selection ---
if (-not $DownloadUrl) {
    # Select Family
    Show-Menu -Title "Select Distribution Family" -Options $DistroCatalog
    do {
        $FamilySelection = Read-Host "Enter choice (1-$($DistroCatalog.Count))"
    } until ($DistroCatalog[$FamilySelection])
    
    $SelectedFamily = $DistroCatalog[$FamilySelection]

    # Select Version
    if ($SelectedFamily.Versions.Count -gt 1) {
        Show-Menu -Title "Select $($SelectedFamily.Name) Version" -Options $SelectedFamily.Versions
        do {
            $VersionSelection = Read-Host "Enter choice (1-$($SelectedFamily.Versions.Count))"
        } until ($SelectedFamily.Versions[$VersionSelection])
        $SelectedVersion = $SelectedFamily.Versions[$VersionSelection]
    } else {
        $SelectedVersion = $SelectedFamily.Versions["1"]
    }

    $DownloadUrl = $SelectedVersion.Url
    Write-Host "`nSelected: $($SelectedVersion.Name)" -ForegroundColor Green
}

# Interactive Name and Path
if (-not $DistroName) {
    $DefaultName = if ($SelectedVersion) { $SelectedVersion.DefaultName } else { "CustomDistro" }
    $InputName = Read-Host "Enter Distro Name [Default: $DefaultName]"
    $DistroName = if ([string]::IsNullOrWhiteSpace($InputName)) { $DefaultName } else { $InputName }
}

if (-not $InstallPath) {
    $DefaultPath = Join-Path $PWD $DistroName
    $InputPath = Read-Host "Enter Install Location [Default: $DefaultPath]"
    $InstallPath = if ([string]::IsNullOrWhiteSpace($InputPath)) { $DefaultPath } else { $InputPath }
}

# --- Installation Logic ---
Write-Host "`n=== Configuration ===" -ForegroundColor Gray
Write-Host "Distro:  $DistroName"
Write-Host "Source:  $DownloadUrl"
Write-Host "Dest:    $InstallPath"
Write-Host "====================="
if (-not $DistroName -or -not $InstallPath -or -not $DownloadUrl) {
    Write-Error "Missing configuration values. Exiting."
    exit 1
}

# Check if Distro Name already exists
$Existing = wsl --list --quiet | Where-Object { $_ -match "$DistroName" }
if ($Existing) {
    Write-Error "A WSL distro with the name '$DistroName' might already exist."
    # Continue? No, error out to be safe.
}

# Create a temporary directory for processing
$TempDir = Join-Path $env:TEMP "WSL_Install_$(Get-Random)"
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
Write-Host "`nPreparing workspace at $TempDir..." -ForegroundColor DarkGray

try {
    # 1. Acquire Package (Cache Check)
    $CachedFile = $null
    
    if ($SelectedVersion -and $SelectedFamily) {
        $BaseDistroDir = Join-Path $PWD "distro"
        $VersionDir = Join-Path $BaseDistroDir "$($SelectedFamily.Name)\$($SelectedVersion.Name)"
        $ExpectedCachePath = Join-Path $VersionDir $SelectedVersion.Filename

        if (Test-Path $ExpectedCachePath) {
            Write-Host "Using cached package: $ExpectedCachePath" -ForegroundColor Green
            $CachedFile = $ExpectedCachePath
        } else {
            Write-Host "Downloading to cache: $ExpectedCachePath" -ForegroundColor Cyan
            if (-not (Test-Path $VersionDir)) { New-Item -Path $VersionDir -ItemType Directory -Force | Out-Null }
            Invoke-WebRequest -Uri $DownloadUrl -OutFile $ExpectedCachePath -UseBasicParsing -Verbose
            $CachedFile = $ExpectedCachePath
        }
    }

    $ProcessingFile = Join-Path $TempDir "distro_package.zip"
    
    if ($CachedFile) {
        Write-Host "Copying to workspace..." -ForegroundColor Gray
        Copy-Item $CachedFile $ProcessingFile
    } else {
        # Fallback for manual URL
        Write-Host "Downloading from URL..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $ProcessingFile -UseBasicParsing -Verbose
    }
    
    # 2. Extract 
    Write-Host "Extracting package..." -ForegroundColor Cyan
    $ExtractDir = Join-Path $TempDir "extracted"
    Expand-Archive -Path $ProcessingFile -DestinationPath $ExtractDir -Force

    # 3. Locate install.tar.gz (Handle Bundles vs Direct Appx)
    # Check 1: Direct RootFS in download
    $RootFs = Join-Path $ExtractDir "install.tar.gz"
    
    if (-not (Test-Path $RootFs)) {
        # Check 2: It's a bundle, find the x64 appx
        $AppxFile = Get-ChildItem -Path $ExtractDir -Filter "*x64*.appx" -Recurse | Select-Object -First 1
        
        if ($AppxFile) {
            Write-Host "Found inner package: $($AppxFile.Name)" -ForegroundColor Gray
            # Rename .appx to .zip and extract again
            $InnerZip = Join-Path $TempDir "inner.zip"
            Copy-Item $AppxFile.FullName $InnerZip
            $InnerDir = Join-Path $TempDir "inner_extracted"
            Expand-Archive -Path $InnerZip -DestinationPath $InnerDir -Force
            $RootFs = Join-Path $InnerDir "install.tar.gz"
        }
    }

    if (-not (Test-Path $RootFs)) {
        throw "Could not find 'install.tar.gz' in the downloaded package. The structure might be different than expected."
    }

    # 4. Create Install Directory
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
    }

    # 5. Import into WSL
    Write-Host "Registering '$DistroName'..." -ForegroundColor Cyan
    wsl --import $DistroName $InstallPath $RootFs --version 2

    Write-Host "`n[SUCCESS] WSL2 Instance '$DistroName' is ready!" -ForegroundColor Green
    Write-Host "Location: $InstallPath"
    Write-Host "To start: wsl -d $DistroName"
    
} catch {
    Write-Error "Installation failed: $_"
} finally {
    Write-Host "Cleaning up..." -ForegroundColor DarkGray
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
