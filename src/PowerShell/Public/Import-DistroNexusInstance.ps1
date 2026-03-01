function Import-DistroNexusInstance {
    <#
    .SYNOPSIS
        Imports a WSL instance from a TAR file.

    .DESCRIPTION
        Validates that the target instance name does not already exist, creates the install
        path if needed, then runs `wsl --import` to register the new instance.

    .PARAMETER Name
        The name for the new WSL instance.

    .PARAMETER Source
        Path to the source TAR file produced by Export-DistroNexusInstance.

    .PARAMETER InstallPath
        Directory where the instance's VHDX will be stored.

    .EXAMPLE
        Import-DistroNexusInstance -Name "Ubuntu-Restored" -Source "D:\Backups\Ubuntu-22.04-20260301.tar" -InstallPath "D:\WSL\Ubuntu-Restored"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string]$Source,

        [Parameter(Mandatory = $true, Position = 2)]
        [ValidateNotNullOrEmpty()]
        [string]$InstallPath
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        # ErrorId = "DistroNexus.ImportFailed"

        # Validate source file exists
        if (-not (Test-Path $Source -PathType Leaf)) {
            Write-Error "Source TAR file '$Source' not found." -ErrorId "DistroNexus.ImportFailed"
            return
        }

        # Validate instance name doesn't already exist
        $existing = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if ($existing) {
            Write-Error "An instance named '$Name' already exists. Choose a different name or remove the existing instance first." `
                -ErrorId "DistroNexus.InstanceAlreadyExists"
            return
        }

        # Create install path if needed
        if (-not (Test-Path $InstallPath)) {
            Write-Verbose "Creating install path '$InstallPath'..."
            New-Item -Path $InstallPath -ItemType Directory -Force | Out-Null
        }

        Write-DistroNexusLog "Importing '$Name' from '$Source' to '$InstallPath'..."
        Write-Verbose "Importing WSL instance '$Name'..."

        $output = wsl --import $Name $InstallPath $Source 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Import failed for '$Name': $output" -ErrorId "DistroNexus.ImportFailed"
            return
        }

        Write-DistroNexusLog "Import complete: '$Name'" -FileOnly
        Write-Verbose "Import complete."

        return [PSCustomObject]@{
            PSTypeName  = 'DistroNexus.ImportResult'
            Name        = $Name
            Source      = $Source
            InstallPath = $InstallPath
            Success     = $true
        }
    }
}
