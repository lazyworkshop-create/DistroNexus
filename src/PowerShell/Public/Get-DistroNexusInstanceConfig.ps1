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

    process {
        Get-DistroNexusInstanceResources -Name $Name
    }
}
