function Set-DistroNexusInstanceTag {
    <#
    .SYNOPSIS
        Replaces all tags for a WSL instance with the provided set.

    .PARAMETER Name
        The WSL instance name.

    .PARAMETER Tags
        The complete set of tags to assign. Max 10 tags; normalised to lowercase.

    .EXAMPLE
        Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("dev","docker")
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [AllowEmptyCollection()]
        [string[]]$Tags
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        if ($Tags.Count -gt 10) {
            Write-Error "Maximum 10 tags allowed per instance. Got $($Tags.Count)." -ErrorId "DistroNexus.TooManyTags"
            return
        }

        $normalised = @($Tags | ForEach-Object { $_.ToLowerInvariant().Trim() } | Select-Object -Unique)

        Set-InstanceTagEntry -Name $Name -Tags $normalised

        Write-DistroNexusLog "Set tags for '$Name': $($normalised -join ', ')"
    }
}
