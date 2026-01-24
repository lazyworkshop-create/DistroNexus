# PowerShell script to download all available WSL2 distro packages
# Author: GitHub Copilot

param (
    [string]$SelectFamily,
    [string]$SelectVersion
)

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "download.log"

# Ensure .NET Http Assembly is loaded for PS 5.1
try {
    if ($PSVersionTable.PSVersion.Major -le 5) {
        Add-Type -AssemblyName System.Net.Http
    }
} catch {
    $warnMsg = "Could not load System.Net.Http assembly: $_"
    Log-Message "WARNING: $warnMsg"
    Write-Warning $warnMsg
}

# --- Distro Definitions ---
$ConfigPath = Join-Path $PSScriptRoot "..\config\distros.json"
if (-not (Test-Path $ConfigPath)) { throw "Config file not found at: $ConfigPath" }
$ConfigRaw = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
$ConfigChanged = $false

$SettingsPath = Join-Path $PSScriptRoot "..\config\settings.json"
$GlobalSettings = $null
if (Test-Path $SettingsPath) {
    try {
        $GlobalSettings = Get-Content -Raw -Path $SettingsPath | ConvertFrom-Json
    } catch {
        Write-Warning "Failed to load settings.json"
    }
}

$DistroCatalog = [ordered]@{}
# Reconstruct ordered dictionary
# $ConfigRaw can be a single PSCustomObject or a dictionary-like object depending on JSON structure
# We iterate over property names
$Keys = $ConfigRaw.PSObject.Properties.Name | Sort-Object
foreach ($Key in $Keys) {
    # Skip potential non-distro properties if any, though usually json is clean
    $FamObj = $ConfigRaw.$Key
    
    # Check if FamObj has 'Versions' property to ensure it's a DistroConfig
    if (-not $FamObj.PSObject.Properties['Versions']) { continue }

    $VersionsDict = [ordered]@{}
    $VerKeys = $FamObj.Versions.PSObject.Properties.Name | Sort-Object
    foreach ($vKey in $VerKeys) {
        $VersionsDict[$vKey] = $FamObj.Versions.$vKey
    }
    
    $DistroCatalog[$Key] = @{
        Name = $FamObj.Name
        Versions = $VersionsDict
    }
}

$BaseDir = Join-Path $PSScriptRoot "..\..\distro"
if ($GlobalSettings.DistroCachePath) {
    if ([System.IO.Path]::IsPathRooted($GlobalSettings.DistroCachePath)) {
        $BaseDir = $GlobalSettings.DistroCachePath
    } else {
        $BaseDir = Join-Path $PSScriptRoot $GlobalSettings.DistroCachePath
    }
}
# Normalize path
$BaseDir = [System.IO.Path]::GetFullPath($BaseDir)

Log-Message "=== WSL Distro Downloader ==="
Log-Message "Base Directory: $BaseDir"
if ($SelectFamily) { Log-Message "Filtering Family: [$SelectFamily]" }
if ($SelectVersion) { Log-Message "Filtering Version: [$SelectVersion]" }
Log-Message "============================`n"

foreach ($FamilyKey in $DistroCatalog.Keys) {
    if ($SelectFamily -and "$FamilyKey" -ne "$SelectFamily") { 
        # Write-Host "Skipping Family: $FamilyKey (neq $SelectFamily)"
        continue 
    }

    $Family = $DistroCatalog[$FamilyKey]
    
    foreach ($VerKey in $Family.Versions.Keys) {
        if ($SelectVersion -and "$VerKey" -ne "$SelectVersion") { 
            # Write-Host "Skipping Version: $VerKey (neq $SelectVersion)"
            continue 
        }

        $Version = $Family.Versions[$VerKey]
        
        # Organization: distro/FamilyName/VersionName/File
        $TargetDir = Join-Path $BaseDir $Family.Name
        $TargetDir = Join-Path $TargetDir $Version.Name
        
        if (-not (Test-Path $TargetDir)) {
            New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
        }

        $OutFile = Join-Path $TargetDir $Version.Filename
        
        Log-Message "[$($Family.Name)] $($Version.Name)"

        $FileExists = Test-Path $OutFile
        if ($FileExists) {
            Log-Message " -> Skipped (Already exists)" "WARN"
        } else {
            Log-Message " -> Downloading..."
            try {
                # Use .NET HttpClient for better progress tracking
                $httpClient = New-Object System.Net.Http.HttpClient
                # 1 = ResponseHeadersRead
                $responseTask = $httpClient.GetAsync($Version.Url, 1)
                $responseTask.Wait()
                $response = $responseTask.Result

                if ($response.IsSuccessStatusCode) {
                    $totalBytes = $response.Content.Headers.ContentLength
                    $streamTask = $response.Content.ReadAsStreamAsync()
                    $streamTask.Wait()
                    $stream = $streamTask.Result
                    
                    $fileStream = [System.IO.File]::Create($OutFile)
                    $buffer = New-Object byte[] 81920 # 80KB buffer
                    $totalRead = 0
                    $lastPercent = -1

                    try {
                        do {
                            $read = $stream.Read($buffer, 0, $buffer.Length)
                            $fileStream.Write($buffer, 0, $read)
                            $totalRead += $read
                            
                            if ($totalBytes -gt 0) {
                                $percent = [Math]::Floor(($totalRead / $totalBytes) * 100)
                                if ($percent -gt $lastPercent -and $percent % 5 -eq 0) {
                                    $mbRed = "{0:N2}" -f ($totalRead / 1MB)
                                    $mbTotal = "{0:N2}" -f ($totalBytes / 1MB)
                                    $msg = "    Progress: $percent% ($mbRed MB / $mbTotal MB)"
                                    if ($percent % 20 -eq 0) { Log-Message $msg }
                                    $lastPercent = $percent
                                }
                            }
                        } while ($read -gt 0)
                    } finally {
                        $fileStream.Close()
                        $stream.Close()
                        $httpClient.Dispose()
                    }
                    $FileExists = $true
                    Log-Message "    Download Completed."
                } else {
                    throw "HTTP Status: $($response.StatusCode)"
                }
            } catch {
                Log-Message " -> Failed: $_" "ERROR"
                if (Test-Path $OutFile) { Remove-Item $OutFile }
            }
        }

        # Update Config if path changed or is new
        if ($FileExists) {
            $CurrentSavedPath = $ConfigRaw.$FamilyKey.Versions.$VerKey.LocalPath
            if ($CurrentSavedPath -ne $OutFile) {
                # We need to assign to the PSObject
                $ConfigRaw.$FamilyKey.Versions.$VerKey | Add-Member -MemberType NoteProperty -Name "LocalPath" -Value $OutFile -Force
                $ConfigChanged = $true
            }
        }
    }
}

if ($ConfigChanged) {
    Log-Message "Updating configuration file with local paths..."
    $JsonOutput = $ConfigRaw | ConvertTo-Json -Depth 6
    if ($JsonOutput) {
        Set-Content -Path $ConfigPath -Value $JsonOutput -Encoding UTF8
    }
}
