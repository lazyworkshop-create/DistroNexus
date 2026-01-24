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

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "install.log"

Log-Message "Starting installation script..."

# --- Distro Definitions ---
$ConfigPath = Join-Path $PSScriptRoot "..\config\distros.json"
if (-not (Test-Path $ConfigPath)) { throw "Config file not found at: $ConfigPath" }

$SettingsPath = Join-Path $PSScriptRoot "..\config\settings.json"
$GlobalSettings = $null
if (Test-Path $SettingsPath) {
    try {
        $GlobalSettings = Get-Content -Raw -Path $SettingsPath | ConvertFrom-Json
    } catch {
        Log-Message "Failed to load settings.json: $_" "WARN"
    }
}

try {
    $JsonRaw = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
} catch {
    # Logging failure is critical here
    Log-Message "Failed to parse distros.json" "ERROR"
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
    Log-Message "Listing available distributions..."
    Write-Host "Available Distributions:" -ForegroundColor Cyan
    foreach ($FamKey in $DistroCatalog.Keys) {
        $Family = $DistroCatalog[$FamKey]
        Write-Host "  $($Family.Name)" -ForegroundColor Yellow
        foreach ($VerKey in $Family.Versions.Keys) {
            $info = $Family.Versions[$VerKey]
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
            $errMsg = "The configured DefaultDistro '$($GlobalSettings.DefaultDistro)' was not found in distros.json."
            Log-Message "ERROR: $errMsg"
            Write-Error $errMsg
            exit 1
    }

    $SelectedVersion = $MatchedVersion
    $SelectedFamily = $MatchedFamily
    
    Log-Message "Quick Mode: Installing Default Distro [$($SelectedVersion.Name)] as ['$TargetDistroName']"
    $DownloadUrl = $SelectedVersion.Url
    
    # Auto-fill missing params if in Quick Mode
    if (-not $DistroName) { 
        $DistroName = $TargetDistroName
    }
    if (-not $InstallPath) { 
        $BasePath = if ($GlobalSettings.DefaultInstallPath) { $GlobalSettings.DefaultInstallPath } else { $PWD }
        $InstallPath = Join-Path $BasePath $DistroName 
        Log-Message "Auto-Path: $InstallPath"
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
    # 1. Try as exact Key (ID)
    if ($DistroCatalog.Contains($SelectFamily)) {
        $SelectedFamilyKey = $SelectFamily
        $SelectedFamily = $DistroCatalog[$SelectFamily]
    } else {
        # 2. Try as Name
        $FoundFamilyKey = $DistroCatalog.Keys | Where-Object { $DistroCatalog[$_].Name -eq $SelectFamily } | Select-Object -First 1
        if ($FoundFamilyKey) {
            $SelectedFamilyKey = $FoundFamilyKey
            $SelectedFamily = $DistroCatalog[$FoundFamilyKey]
        }
    }

    if ($SelectedFamily) {
        if ($SelectVersion) {
            # 1. Try as exact Key (ID)
            if ($SelectedFamily.Versions.Contains($SelectVersion)) {
                 $SelectedVersionKey = $SelectVersion
                 $SelectedVersion = $SelectedFamily.Versions[$SelectVersion]
            } else {
                # 2. Try as Name (NavName)
                $FoundVersionKey = $SelectedFamily.Versions.Keys | Where-Object { $SelectedFamily.Versions[$_].NavName -eq $SelectVersion } | Select-Object -First 1
                
                # 3. Try partial Name match if exact not found
                if (-not $FoundVersionKey) {
                    $FoundVersionKey = $SelectedFamily.Versions.Keys | Where-Object { $SelectedFamily.Versions[$_].Name -like "*$SelectVersion*" } | Select-Object -First 1
                }
                
                if ($FoundVersionKey) {
                    $SelectedVersionKey = $FoundVersionKey
                    $SelectedVersion = $SelectedFamily.Versions[$FoundVersionKey]
                }
            }
            
            if (-not $SelectedVersion) {
                 $warnMsg = "Version '$SelectVersion' not found for $($SelectedFamily.Name). Using default."
                 Log-Message "WARNING: $warnMsg"
                 Write-Warning $warnMsg
                 $SelectedVersionKey = "1"
                 $SelectedVersion = $SelectedFamily.Versions["1"]
            }
        } else {
            $SelectedVersionKey = "1"
            $SelectedVersion = $SelectedFamily.Versions["1"]
        }
        $DownloadUrl = $SelectedVersion.Url
        Log-Message "Auto-Selected: $($SelectedVersion.Name)"
    } else {
        $warnMsg = "Family '$SelectFamily' not found."
        Log-Message "WARNING: $warnMsg"
        Write-Warning $warnMsg
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
    Log-Message "Selected: $($SelectedVersion.Name)"
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
    Log-Message "Distro Name: $DistroName"
}

if (-not $InstallPath) {
    $BasePath = if ($GlobalSettings.DefaultInstallPath) { $GlobalSettings.DefaultInstallPath } else { $PWD }
    $DefaultPath = Join-Path $BasePath $DistroName
    $InputPath = Read-Host "Enter Install Location [Default: $DefaultPath]"
    $InstallPath = if ([string]::IsNullOrWhiteSpace($InputPath)) { $DefaultPath } else { $InputPath }
    Log-Message "Install Path: $InstallPath"
}

# --- Installation Logic ---
Log-Message "`n=== Configuration ==="
Log-Message "Distro:  $DistroName"
Log-Message "Source:  $DownloadUrl"
Log-Message "Dest:    $InstallPath"
Log-Message "====================="
if (-not $DistroName -or -not $InstallPath -or -not $DownloadUrl) {
    $errMsg = "Missing configuration values. Exiting."
    Log-Message "ERROR: $errMsg"
    Write-Error $errMsg
    exit 1
}

# Check if Distro Name already exists
$Existing = wsl --list --quiet | Where-Object { $_ -match "$DistroName" }
if ($Existing) {
    $errMsg = "A WSL distro with the name '$DistroName' might already exist."
    Log-Message "ERROR: $errMsg"
    Write-Error $errMsg
    # Continue? No, error out to be safe.
}

# Create a temporary directory for processing
$TempDir = Join-Path $env:TEMP "WSL_Install_$(Get-Random)"
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
Log-Message "Preparing workspace at $TempDir..."

try {
    # 1. Acquire Package (Cache Check)
    $CachedFile = $null
    $SourcePath = $null
    
    # Priority 1: Check LocalPath from Config
    if ($SelectedVersion.LocalPath) {
        if (Test-Path $SelectedVersion.LocalPath) {
            $SourcePath = $SelectedVersion.LocalPath
            Log-Message "Using registered local copy: $SourcePath"
        } else {
             $warnMsg = "Registered LocalPath '$($SelectedVersion.LocalPath)' not found."
             Log-Message "WARNING: $warnMsg"
             Write-Warning $warnMsg
        }
    }

    # Priority 2: Use Download Manager to Find/Download
    if (-not $SourcePath -and $SelectedFamilyKey -and $SelectedVersionKey) {
        Log-Message "Checking download status (invoking download manager)..."
        & "$PSScriptRoot\download_all_distros.ps1" -SelectFamily $SelectedFamilyKey -SelectVersion $SelectedVersionKey
        
        # Reload Config to get updated path from disk
        if (Test-Path $ConfigPath) {
             try {
                 $RawJson = Get-Content $ConfigPath -Raw | ConvertFrom-Json
                 $UpdatedFamily = $RawJson.distros | Where-Object { $_.key -eq $SelectedFamilyKey }
                 $UpdatedVersion = $UpdatedFamily.versions | Where-Object { $_.key -eq $SelectedVersionKey }
                 
                 if ($UpdatedVersion.local_path -and (Test-Path $UpdatedVersion.local_path)) {
                     $SourcePath = $UpdatedVersion.local_path
                     $SelectedVersion.LocalPath = $SourcePath
                     Log-Message "Package ready: $SourcePath"
                 }
             } catch {
                 $warnMsg = "Failed to reload config: $_"
                 Log-Message "WARNING: $warnMsg"
                 Write-Warning $warnMsg
             }
        }
    }

    if ($SourcePath) {
        $CachedFile = $SourcePath
    }

    # Determine file type based on extension
    $SourceFileName = if ($CachedFile) { [System.IO.Path]::GetFileName($CachedFile) } else { $null }
    
    if (-not $SourceFileName -and $DownloadUrl) {
        # Try to infer filename from URL if not cached
        try {
            $uri = [System.Uri]$DownloadUrl
            $SourceFileName = [System.IO.Path]::GetFileName($uri.LocalPath)
        } catch {}
        if (-not $SourceFileName) { $SourceFileName = "downloaded_distro.tar.gz" }
    }

    $ProcessingFile = Join-Path $TempDir $SourceFileName
    
    if ($CachedFile) {
        Log-Message "Copying to workspace..."
        Copy-Item $CachedFile $ProcessingFile
    } else {
        # Fallback for manual URL
        Log-Message "Downloading from URL..."
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $ProcessingFile -UseBasicParsing
    }
    
    $RootFs = $null

    # Check for known Archive types that need extraction (Appx, Zip)
    if ($ProcessingFile -match "\.(zip|appx|appxbundle)$") {
        # 2. Extract 
        Log-Message "Extracting package..."
        $ExtractDir = Join-Path $TempDir "extracted"
        Expand-Archive -Path $ProcessingFile -DestinationPath $ExtractDir -Force

        # 3. Locate install.tar.gz (Handle Bundles vs Direct Appx)
        # Check 1: Direct RootFS in download
        $RootFs = Join-Path $ExtractDir "install.tar.gz"
        
        if (-not (Test-Path $RootFs)) {
            # Check 2: It's a bundle, find the x64 appx
            $AppxFile = Get-ChildItem -Path $ExtractDir -Filter "*x64*.appx" -Recurse | Select-Object -First 1
            
            if ($AppxFile) {
                Log-Message "Found inner package: $($AppxFile.Name)"
                # Rename .appx to .zip and extract again
                $InnerZip = Join-Path $TempDir "inner.zip"
                Copy-Item $AppxFile.FullName $InnerZip
                $InnerDir = Join-Path $TempDir "inner_extracted"
                Expand-Archive -Path $InnerZip -DestinationPath $InnerDir -Force
                $RootFs = Join-Path $InnerDir "install.tar.gz"
            }
        }
    } else {
        # Assume it is already a RootFS (tar.gz, .wsl, etc.)
        Log-Message "Package recognized as direct RootFS ($([System.IO.Path]::GetExtension($ProcessingFile)))..."
        $RootFs = $ProcessingFile
    }

    if (-not $RootFs -or -not (Test-Path $RootFs)) {
        throw "Could not find 'install.tar.gz' or valid RootFS in the package."
    }

    # 4. Create Install Directory
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
    }

    # 5. Import into WSL
    Log-Message "Registering '$DistroName'..."
    wsl --import $DistroName $InstallPath $RootFs --version 2

    # 6. User Setup (if requested)
    if ($user) {
        Log-Message "Setting up user '$user'..."
        
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
        
        Log-Message "User '$user' configured as default."
        
        # Terminate to ensure next start picks up the config? Defaults usually apply on next session.
        wsl --terminate $DistroName
    }

    # 7. Update Instances Configuration
    $InstancesConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
    $NewInstance = @{
        Name = $DistroName
        BasePath = $InstallPath
        State = "Stopped"
        WslVer = "2"
        Release = if ($SelectedVersion) { $SelectedVersion.Name } else { "Custom" }
        User = if ($user) { $user } else { "root" }
        InstallTime = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }

    $CurrentInstances = @()
    if (Test-Path $InstancesConfigPath) {
        try {
            $CurrentInstances = Get-Content $InstancesConfigPath -Raw | ConvertFrom-Json
            if (-not $CurrentInstances) { $CurrentInstances = @() }
            # Force to array if single object
            if ($CurrentInstances -isnot [System.Array]) { $CurrentInstances = @($CurrentInstances) }
        } catch {
            $warnMsg = "Could not read existing instances.json. Starting fresh."
            Log-Message "WARNING: $warnMsg"
            Write-Warning $warnMsg
        }
    }
    
    # Remove duplicates if reinstalling with same name
    $CurrentInstances = $CurrentInstances | Where-Object { $_.Name -ne $DistroName }
    $CurrentInstances += $NewInstance
    
    $CurrentInstances | ConvertTo-Json -Depth 4 | Set-Content $InstancesConfigPath -Force
    Log-Message "Updated instances registry."

    Log-Message "`n[SUCCESS] WSL2 Instance '$DistroName' is ready!"
    Log-Message "Location: $InstallPath"
    Log-Message "To start: wsl -d $DistroName"
    
} catch {
    $err = "Installation failed: $_"
    Log-Message "ERROR: $err"
    Write-Error $err
} finally {
    Log-Message "Cleaning up..."
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
