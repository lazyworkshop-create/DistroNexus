function Remove-DistroNexusInstanceTag {
    <#
    .SYNOPSIS
        Removes a single tag from a WSL instance.

    .PARAMETER Name
        The WSL instance name.

    .PARAMETER Tag
        The tag to remove (case-insensitive).

    .EXAMPLE
        Remove-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "docker"
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string]$Tag
    )

    process {
        $normalised = $Tag.ToLowerInvariant().Trim()
        $tagMap     = Get-InstanceTagMap
        $existing   = if ($tagMap.PSObject.Properties[$Name]) { @($tagMap.$Name) } else { @() }

        $updated = @($existing | Where-Object { $_ -ne $normalised })
        if (-not $PSCmdlet.ShouldProcess($Name, "Remove tag '$normalised'")) {
            return
        }
        Initialize-DistroNexusLogger
        Set-InstanceTagEntry -Name $Name -Tags $updated

        Write-DistroNexusLog "Removed tag '$normalised' from '$Name' (if present)"
    }
}
