function Get-DistroNexusConfig {
    <#
    .SYNOPSIS
        Loads configuration from catalog.json and settings.json.

    .DESCRIPTION
        Internal helper function to load distro catalog and global settings.
        Returns a hashtable containing both configurations.

    .PARAMETER ConfigRoot
        Root directory containing config files. If not specified, uses module root/../config

    .EXAMPLE
        $config = Get-DistroNexusConfig
        $config.Distros
        $config.Settings
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$ConfigRoot
    )
    
    if (-not $ConfigRoot) {
        # Use AppData for configuration files (consistent with C# application)
        $appDataRoot = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::ApplicationData)
        $ConfigRoot = Join-Path $appDataRoot "DistroNexus"
    }
    
    $result = @{
        Distros = $null
        Settings = $null
    }
    
    # Load catalog.json
    $catalogPath = Join-Path $ConfigRoot "catalog.json"
    
    if (Test-Path $catalogPath) {
        try {
            $distrosContent = Get-Content -Raw -Path $catalogPath | ConvertFrom-Json
            $result.Distros = $distrosContent
            Write-Verbose "Loaded catalog from $catalogPath"
        }
        catch {
            Write-DistroNexusLog "Failed to load catalog: $_" -Level ERROR
            throw "Failed to parse catalog. Please ensure it is valid JSON."
        }
    }
    else {
        Write-DistroNexusLog "Catalog not found at $catalogPath" -Level WARN
    }
    
    # Load settings.json
    $settingsPath = Join-Path $ConfigRoot "settings.json"
    if (Test-Path $settingsPath) {
        try {
            $settingsContent = Get-Content -Raw -Path $settingsPath | ConvertFrom-Json
            $result.Settings = $settingsContent
            Write-Verbose "Loaded settings from $settingsPath"
        }
        catch {
            Write-DistroNexusLog "Failed to load settings.json: $_" -Level WARN
        }
    }
    else {
        Write-DistroNexusLog "Settings file not found at $settingsPath" -Level WARN
    }
    
    return $result
}

function Save-DistroNexusSettings {
    <#
    .SYNOPSIS
        Saves global settings to settings.json.

    .DESCRIPTION
        Internal helper function to persist settings changes.

    .PARAMETER Settings
        Settings object to save.

    .PARAMETER ConfigRoot
        Root directory for config files. If not specified, uses AppData/DistroNexus
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$Settings,
        
        [Parameter(Mandatory = $false)]
        [string]$ConfigRoot
    )
    
    if (-not $ConfigRoot) {
        # Use AppData for configuration files (consistent with C# application)
        $appDataRoot = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::ApplicationData)
        $ConfigRoot = Join-Path $appDataRoot "DistroNexus"
    }
    
    # Ensure directory exists
    if (-not (Test-Path $ConfigRoot)) {
        New-Item -ItemType Directory -Path $ConfigRoot -Force | Out-Null
    }
    
    $settingsPath = Join-Path $ConfigRoot "settings.json"
    
    try {
        $Settings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Force
        Write-Verbose "Settings saved to $settingsPath"
    }
    catch {
        Write-DistroNexusLog "Failed to save settings: $_" -Level ERROR
        throw
    }
}
