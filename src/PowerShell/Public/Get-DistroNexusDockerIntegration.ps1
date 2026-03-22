function Get-DistroNexusDockerIntegration {
    <#
    .SYNOPSIS
        Gets the Docker Desktop WSL 2 integration status for a WSL instance.

    .DESCRIPTION
        Reads the Docker Desktop settings JSON (settings-store.json for 4.30+,
        falling back to settings.json) and reports whether the given instance
        is listed in the integratedWslDistros array.

    .PARAMETER Name
        The name of the WSL instance to query.

    .OUTPUTS
        PSCustomObject with InstanceName, Status (Enabled|Disabled|Unavailable),
        DockerInstalled, and SettingsPath properties.

    .EXAMPLE
        Get-DistroNexusDockerIntegration -Name "Ubuntu-22.04"
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        # ErrorId = "DistroNexus.GetDockerIntegration"

        # Reserved Docker-internal distros
        if ($Name -eq "docker-desktop" -or $Name -eq "docker-desktop-data") {
            return [PSCustomObject]@{
                InstanceName    = $Name
                Status          = "Unavailable"
                DockerInstalled = $false
                SettingsPath    = $null
            }
        }

        # Check Docker Desktop installation
        $dockerExe = Join-Path $env:LOCALAPPDATA "Docker\Docker Desktop.exe"
        $dockerInstalled = Test-Path $dockerExe

        if (-not $dockerInstalled) {
            return [PSCustomObject]@{
                InstanceName    = $Name
                Status          = "Unavailable"
                DockerInstalled = $false
                SettingsPath    = $null
            }
        }

        # Resolve settings path
        $dockerAppData = Join-Path $env:APPDATA "Docker"
        $settingsPath  = Join-Path $dockerAppData "settings-store.json"
        if (-not (Test-Path $settingsPath)) {
            $settingsPath = Join-Path $dockerAppData "settings.json"
        }

        if (-not (Test-Path $settingsPath)) {
            return [PSCustomObject]@{
                InstanceName    = $Name
                Status          = "Unavailable"
                DockerInstalled = $true
                SettingsPath    = $null
            }
        }

        try {
            $settings = Get-Content -Raw -Path $settingsPath | ConvertFrom-Json
            $distros  = @($settings.integratedWslDistros)
            $status   = if ($distros -contains $Name) { "Enabled" } else { "Disabled" }

            return [PSCustomObject]@{
                InstanceName    = $Name
                Status          = $status
                DockerInstalled = $true
                SettingsPath    = $settingsPath
            }
        }
        catch {
            Write-DistroNexusLog "Failed to read Docker settings: $_" -Level WARN
            return [PSCustomObject]@{
                InstanceName    = $Name
                Status          = "Unavailable"
                DockerInstalled = $true
                SettingsPath    = $settingsPath
            }
        }
    }
}
