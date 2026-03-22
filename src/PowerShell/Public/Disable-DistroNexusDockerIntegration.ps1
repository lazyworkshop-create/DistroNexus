function Disable-DistroNexusDockerIntegration {
    <#
    .SYNOPSIS
        Disables Docker Desktop WSL 2 integration for a WSL instance.

    .DESCRIPTION
        Removes the specified instance from the integratedWslDistros array in
        Docker's settings JSON file.

    .PARAMETER Name
        The name of the WSL instance to disable integration for.

    .EXAMPLE
        Disable-DistroNexusDockerIntegration -Name "Ubuntu-22.04"
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
        # ErrorId = "DistroNexus.DisableDockerIntegration"

        $dockerExe = Join-Path $env:LOCALAPPDATA "Docker\Docker Desktop.exe"
        if (-not (Test-Path $dockerExe)) {
            Write-Error "Docker Desktop is not installed (expected at $dockerExe)." `
                -ErrorId "DistroNexus.DockerDesktopNotFound"
            return
        }

        $dockerDir  = Join-Path $env:APPDATA "Docker"
        $settingsPath = Join-Path $dockerDir "settings-store.json"
        if (-not (Test-Path $settingsPath)) {
            $settingsPath = Join-Path $dockerDir "settings.json"
        }

        if (-not (Test-Path $settingsPath)) {
            Write-Verbose "Docker settings file not found; nothing to disable."
            return
        }

        try {
            $settings = Get-Content -Raw -Path $settingsPath | ConvertFrom-Json
            $distros  = [System.Collections.Generic.List[string]]@()
            if ($settings.integratedWslDistros) {
                $distros.AddRange([string[]]$settings.integratedWslDistros)
            }

            $distros.RemoveAll([System.Predicate[string]]{ param($n) $n -eq $Name }) | Out-Null

            $settings | Add-Member -NotePropertyName integratedWslDistros -NotePropertyValue $distros.ToArray() -Force
            $settings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Encoding UTF8 -Force
            Write-DistroNexusLog "Docker integration disabled for '$Name'" -FileOnly
            Write-Verbose "Docker Desktop integration disabled for '$Name'."
            Write-Warning "Docker Desktop must be restarted for this change to take effect."
        }
        catch {
            Write-Error "Failed to update Docker settings: $_" -ErrorId "DistroNexus.DockerConfigWriteConflict"
        }
    }
}
