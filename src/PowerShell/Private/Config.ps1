function Get-DistroNexusConfig {
    <#
    .SYNOPSIS
        Loads configuration from distros.json and settings.json.

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
        $ConfigRoot = Join-Path $script:ModuleRoot "..\config"
    }
    
    $result = @{
        Distros = $null
        Settings = $null
    }
    
    # Load distros.json
    $distrosPath = Join-Path $ConfigRoot "distros.json"
    if (Test-Path $distrosPath) {
        try {
            $distrosContent = Get-Content -Raw -Path $distrosPath | ConvertFrom-Json
            $result.Distros = $distrosContent
            Write-Verbose "Loaded distros catalog from $distrosPath"
        }
        catch {
            Write-DistroNexusLog "Failed to load distros.json: $_" -Level ERROR
            throw "Failed to parse distros.json. Please ensure it is valid JSON."
        }
    }
    else {
        Write-DistroNexusLog "Distros catalog not found at $distrosPath" -Level WARN
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
        Root directory for config files. If not specified, uses module root/../config
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$Settings,
        
        [Parameter(Mandatory = $false)]
        [string]$ConfigRoot
    )
    
    if (-not $ConfigRoot) {
        $ConfigRoot = Join-Path $script:ModuleRoot "..\config"
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
