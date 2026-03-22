function Add-DistroNexusInstanceTag {
    <#
    .SYNOPSIS
        Adds a single tag to a WSL instance.

    .PARAMETER Name
        The WSL instance name.

    .PARAMETER Tag
        The tag to add (normalised to lowercase; ignored if already present).

    .EXAMPLE
        Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "docker"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [ValidateLength(1, 32)]
        [string]$Tag
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $normalised = $Tag.ToLowerInvariant().Trim()
        $tagMap     = Get-InstanceTagMap
        $existing   = if ($tagMap.PSObject.Properties[$Name]) { @($tagMap.$Name) } else { @() }

        if ($existing.Count -ge 10) {
            Write-Error "Instance '$Name' already has 10 tags (maximum). Remove a tag before adding a new one." `
                -ErrorId "DistroNexus.TooManyTags"
            return
        }

        if ($existing -notcontains $normalised) {
            $updated = @($existing) + $normalised
            Set-InstanceTagEntry -Name $Name -Tags $updated
            Write-DistroNexusLog "Added tag '$normalised' to '$Name'"
        }
    }
}
