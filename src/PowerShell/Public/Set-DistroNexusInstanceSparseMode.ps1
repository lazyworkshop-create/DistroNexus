function Set-DistroNexusInstanceSparseMode {
    <#
    .SYNOPSIS
        Sets the sparse VHDX mode for a WSL2 instance.

    .DESCRIPTION
        Runs `wsl --manage <Name> --set-sparse <true|false>`. Guards against WSL v1 instances.

    .PARAMETER Name
        The name of the WSL instance.

    .PARAMETER Enabled
        $true to enable sparse mode, $false to disable.

    .EXAMPLE
        Set-DistroNexusInstanceSparseMode -Name "Ubuntu-22.04" -Enabled $true
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [bool]$Enabled
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        # ErrorId = "DistroNexus.WslVersionTooLow"

        $instance = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-Error "Instance '$Name' not found." -ErrorId "DistroNexus.InstanceNotFound"
            return
        }

        # Guard: WSL v1 does not support sparse mode
        if ($instance.Version -eq 1) {
            Write-Error "Sparse mode is only supported on WSL 2 instances. '$Name' is a WSL 1 instance." `
                -ErrorId "DistroNexus.WslVersionTooLow"
            return
        }

        $sparseArg = if ($Enabled) { "true" } else { "false" }
        Write-Verbose "Setting sparse mode to $sparseArg for '$Name'..."

        $output = wsl --manage $Name --set-sparse $sparseArg 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to set sparse mode for '$Name': $output" `
                -ErrorId "DistroNexus.WslConfigWriteFailed"
            return
        }

        Write-DistroNexusLog "Sparse mode set to $sparseArg for '$Name'" -FileOnly
        Write-Verbose "Sparse mode updated for '$Name'."
    }
}
