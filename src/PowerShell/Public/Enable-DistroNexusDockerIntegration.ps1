function Enable-DistroNexusDockerIntegration {
    <#
    .SYNOPSIS
        Enables Docker Desktop WSL 2 integration for a WSL instance.

    .DESCRIPTION
        Adds the specified instance to the integratedWslDistros array in Docker's
        settings JSON file. WSL v1 instances are rejected. Reserved Docker distros
        (docker-desktop, docker-desktop-data) are skipped.

    .PARAMETER Name
        The name of the WSL instance to enable integration for.

    .EXAMPLE
        Enable-DistroNexusDockerIntegration -Name "Ubuntu-22.04"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        # ErrorId = "DistroNexus.EnableDockerIntegration"

        if ($Name -eq "docker-desktop" -or $Name -eq "docker-desktop-data") {
            Write-Error "Cannot enable Docker integration for reserved distro '$Name'." `
                -ErrorId "DistroNexus.InstanceNotFound"
            return
        }

        $dockerExe = Join-Path $env:LOCALAPPDATA "Docker\Docker Desktop.exe"
        if (-not (Test-Path $dockerExe)) {
            Write-Error "Docker Desktop is not installed (expected at $dockerExe)." `
                -ErrorId "DistroNexus.DockerDesktopNotFound"
            return
        }

        # Guard: reject WSL v1 instances
        $instance = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if ($instance -and $instance.Version -eq 1) {
            Write-Error "Cannot enable Docker integration for WSL v1 instance '$Name'. Convert to WSL 2 first." `
                -ErrorId "DistroNexus.WslVersionTooLow"
            return
        }

        $settingsPath = Resolve-DockerSettingsPath
        if (-not (Test-Path $settingsPath)) {
            # Create minimal settings file
            $dockerDir = Split-Path $settingsPath
            if (-not (Test-Path $dockerDir)) {
                New-Item -Path $dockerDir -ItemType Directory -Force | Out-Null
            }
            @{ integratedWslDistros = @() } | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding UTF8
        }

        try {
            $settings = Get-Content -Raw -Path $settingsPath | ConvertFrom-Json
            $distros  = [System.Collections.Generic.List[string]]@()
            if ($settings.integratedWslDistros) {
                $distros.AddRange([string[]]$settings.integratedWslDistros)
            }

            if (-not ($distros -contains $Name)) {
                $distros.Add($Name)
            }

            $settings | Add-Member -NotePropertyName integratedWslDistros -NotePropertyValue $distros.ToArray() -Force
            $settings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Encoding UTF8 -Force
            Write-DistroNexusLog "Docker integration enabled for '$Name'" -FileOnly
            Write-Verbose "Docker Desktop integration enabled for '$Name'."
            Write-Warning "Docker Desktop must be restarted for this change to take effect."
        }
        catch {
            Write-Error "Failed to update Docker settings: $_" -ErrorId "DistroNexus.DockerConfigWriteConflict"
        }
    }
}

function Resolve-DockerSettingsPath {
    $dockerDir = Join-Path $env:APPDATA "Docker"
    $newPath   = Join-Path $dockerDir "settings-store.json"
    $legacyPath = Join-Path $dockerDir "settings.json"
    if (Test-Path $newPath)    { return $newPath }
    if (Test-Path $legacyPath) { return $legacyPath }
    return $newPath   # preferred path for creation
}
