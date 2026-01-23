# PowerShell script to interactively download and create a custom WSL2 instance
# Author: GitHub Copilot

param (
    [string]$DistroName,
    [string]$InstallPath,
    [string]$SelectFamily,
    [string]$SelectVersion,
    [string]$name,
    [string]$user,
    [string]$pass,
    [switch]$List,
    [alias("ls")]
    [switch]$ListAlias
)

if ($ListAlias) { $List = $true }

$ErrorActionPreference = "Stop"

# --- Distro Definitions ---
$ConfigPath = Join-Path $PSScriptRoot "..\config\distros.json"
if (-not (Test-Path $ConfigPath)) { throw "Config file not found at: $ConfigPath" }

$SettingsPath = Join-Path $PSScriptRoot "..\config\settings.json"
$GlobalSettings = $null
if (Test-Path $SettingsPath) {
    try {
        $GlobalSettings = Get-Content -Raw -Path $SettingsPath | ConvertFrom-Json
    } catch {
        Write-Warning "Failed to load settings.json"
    }
}

try {
    $JsonRaw = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
} catch {
    throw "Failed to parse distros.json. Please ensure it is valid JSON."
}

$DistroCatalog = [ordered]@{}
# Reconstruct ordered dictionary to ensure menu consistency
$Keys = $JsonRaw.PSObject.Properties.Name | Sort-Object { [int]$_ }
foreach ($Key in $Keys) {
    $FamObj = $JsonRaw.$Key
    $VersionsDict = [ordered]@{}
    $VerKeys = $FamObj.Versions.PSObject.Properties.Name | Sort-Object { [int]$_ }
    foreach ($vKey in $VerKeys) {
        $VersionsDict[$vKey] = $FamObj.Versions.$vKey
    }
    
    $DistroCatalog[$Key] = @{
        Name = $FamObj.Name
        Versions = $VersionsDict
    }
}

if ($List) {
    Write-Host "Available Distributions:" -ForegroundColor Cyan
    foreach ($key in $DistroCatalog.Keys) {
         $Family = $DistroCatalog[$key]
         Write-Host "  $($Family.Name)" -ForegroundColor Yellow
         foreach ($vKey in $Family.Versions.Keys) {
             $info = $Family.Versions[$vKey]
             Write-Host "    - $($info.Name)"
         }
    }
    exit 0
}

# --- Quick Install Logic ---
if ($name) {
    # Quick Mode Logic:
    # 1. '-name' argument is ALWAYS the desired Distro Name
    # 2. Distro Version is ALWAYS from 'DefaultDistro' in settings.json
    
    $TargetDistroName = $name

    # Locate default distro configured in settings.json
    if (-not $GlobalSettings.DefaultDistro) {
        Write-Error "DefaultDistro key is missing in settings.json. Quick Install mode requires a default distro."
        exit 1
    }

    $MatchedVersion = $null
    $MatchedFamily = $null

    foreach ($fKey in $DistroCatalog.Keys) {
        $fam = $DistroCatalog[$fKey]
        foreach ($vKey in $fam.Versions.Keys) {
            $ver = $fam.Versions[$vKey]
            if ($ver.DefaultName -eq $GlobalSettings.DefaultDistro) {
                $MatchedVersion = $ver
                $MatchedFamily = $fam
                break
            }
        }
        if ($MatchedVersion) { break }
    }

    if (-not $MatchedVersion) {
            Write-Error "The configured DefaultDistro '$($GlobalSettings.DefaultDistro)' was not found in distros.json."
            exit 1
    }

    $SelectedVersion = $MatchedVersion
    $SelectedFamily = $MatchedFamily
    
    Write-Host "Quick Mode: Installing Default Distro [$($SelectedVersion.Name)] as ['$TargetDistroName']" -ForegroundColor Green
    $DownloadUrl = $SelectedVersion.Url
    
    # Auto-fill missing params if in Quick Mode
    if (-not $DistroName) { 
        $DistroName = $TargetDistroName
    }
    if (-not $InstallPath) { 
        $BasePath = if ($GlobalSettings.DefaultInstallPath) { $GlobalSettings.DefaultInstallPath } else { $PWD }
        $InstallPath = Join-Path $BasePath $DistroName 
        Write-Host "Auto-Path: $InstallPath" -ForegroundColor Gray
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

if ($SelectFamily) {
    # Try to find family by name
    $FoundFamilyKey = $DistroCatalog.Keys | Where-Object { $DistroCatalog[$_].Name -eq $SelectFamily } | Select-Object -First 1
    if ($FoundFamilyKey) {
        $SelectedFamily = $DistroCatalog[$FoundFamilyKey]
        
        if ($SelectVersion) {
                $FoundVersionKey = $SelectedFamily.Versions.Keys | Where-Object { $SelectedFamily.Versions[$_].Name -like "*$SelectVersion*" } | Select-Object -First 1
                if ($FoundVersionKey) {
                    $SelectedVersion = $SelectedFamily.Versions[$FoundVersionKey]
                } else {
                    Write-Warning "Version '$SelectVersion' not found for $($SelectedFamily.Name). Using default."
                    $SelectedVersion = $SelectedFamily.Versions["1"]
                }
        } else {
            $SelectedVersion = $SelectedFamily.Versions["1"]
        }
        $DownloadUrl = $SelectedVersion.Url
        Write-Host "Auto-Selected: $($SelectedVersion.Name)" -ForegroundColor Green
    } else {
        Write-Warning "Family '$SelectFamily' not found."
    }
}

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
    if ($GlobalSettings.DefaultDistro -and -not $SelectedVersion) {
         # if nothing selected yet, maybe suggest global default?
         # For now, we only use DefaultDistro effectively if it was passed via QuickInstall logic or similar, but
         # the prompt default logic below is for Name specifically.
    }

    $DefaultName = if ($SelectedVersion) { $SelectedVersion.DefaultName } else { if ($GlobalSettings.DefaultDistro) { $GlobalSettings.DefaultDistro } else { "CustomDistro" } }
    $InputName = Read-Host "Enter Distro Name [Default: $DefaultName]"
    $DistroName = if ([string]::IsNullOrWhiteSpace($InputName)) { $DefaultName } else { $InputName }
}

if (-not $InstallPath) {
    $BasePath = if ($GlobalSettings.DefaultInstallPath) { $GlobalSettings.DefaultInstallPath } else { $PWD }
    $DefaultPath = Join-Path $BasePath $DistroName
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
        $BaseDistroDir = Join-Path $PSScriptRoot "..\..\distro"
        if ($GlobalSettings.DistroCachePath) {
            if ([System.IO.Path]::IsPathRooted($GlobalSettings.DistroCachePath)) {
                $BaseDistroDir = $GlobalSettings.DistroCachePath
            } else {
                $BaseDistroDir = Join-Path $PSScriptRoot $GlobalSettings.DistroCachePath
            }
        }
        $BaseDistroDir = [System.IO.Path]::GetFullPath($BaseDistroDir)

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

    # 6. User Setup (if requested)
    if ($user) {
        Write-Host "Setting up user '$user'..." -ForegroundColor Cyan
        
        # Create user
        # Note: Using 'exec' to run commands directly inside the distro
        wsl -d $DistroName -u root -- exec useradd -m -s /bin/bash $user
        
        # Set password if provided
        if ($pass) {
            # Use chpasswd to set password non-interactively
            wsl -d $DistroName -u root -- exec sh -c "echo '${user}:${pass}' | chpasswd"
        }

        # Add to sudo/wheel groups (Try both to cover Ubuntu/Debian and RHEL/Oracle)
        # Use sh -c with || true to suppress errors if a group doesn't exist (e.g. wheel on Debian)
        wsl -d $DistroName -u root -- sh -c "usermod -aG sudo $user 2>/dev/null || true"
        wsl -d $DistroName -u root -- sh -c "usermod -aG wheel $user 2>/dev/null || true"

        # Set default user in /etc/wsl.conf
        $wslConfContent = "[user]`ndefault=$user"
        # We need to write this file inside the distro.
        # Note: bash -c "echo ... > file" might fail with complex strings, but simple content is fine.
        wsl -d $DistroName -u root -- exec sh -c "printf '[user]\ndefault=$user\n' > /etc/wsl.conf"
        
        Write-Host "User '$user' configured as default." -ForegroundColor Green
        
        # Terminate to ensure next start picks up the config? Defaults usually apply on next session.
        wsl --terminate $DistroName
    }

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
