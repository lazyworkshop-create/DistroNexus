function Get-DistroNexusInstanceConfig {
    <#
    .SYNOPSIS
        Gets the per-instance resource configuration including sparse mode and WSL version.

    .DESCRIPTION
        Reads the SparseVhd registry value from the instance's Lxss key and reads
        global quotas from ~/.wslconfig. Returns a combined view.

    .PARAMETER Name
        The name of the WSL instance.

    .OUTPUTS
        PSCustomObject with Name, SparseMode, WslVersion, GlobalMemory, GlobalCPUs.

    .EXAMPLE
        Get-DistroNexusInstanceConfig -Name "Ubuntu-22.04"
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
        # ErrorId = "DistroNexus.RegistryAccessDenied"

        $instance = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-Error "Instance '$Name' not found." -ErrorId "DistroNexus.InstanceNotFound"
            return
        }

        # Read SparseVhd from registry
        $sparseMode = $false
        try {
            $lxssRoot = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
            $keys     = Get-ChildItem -Path $lxssRoot -ErrorAction SilentlyContinue
            foreach ($key in $keys) {
                try {
                    $props = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
                    if ($props.DistributionName -eq $Name) {
                        $rawSparse = $props.SparseVhd
                        $sparseMode = ($rawSparse -ne $null -and $rawSparse -ne 0)
                        break
                    }
                }
                catch { continue }
            }
        }
        catch {
            Write-DistroNexusLog "Failed to read registry for '$Name': $_" -Level WARN
        }

        # Read global config
        $globalConfig = Get-DistroNexusWslConfig -ErrorAction SilentlyContinue

        return [PSCustomObject]@{
            PSTypeName  = 'DistroNexus.InstanceConfig'
            Name        = $Name
            SparseMode  = $sparseMode
            WslVersion  = $instance.Version
            GlobalMemory = if ($globalConfig) { $globalConfig.Memory } else { $null }
            GlobalCPUs  = if ($globalConfig) { $globalConfig.Processors } else { $null }
        }
    }
}
