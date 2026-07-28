function Get-DistroNexusInstance {
    <#
    .SYNOPSIS
        Gets information about installed WSL instances.

    .DESCRIPTION
        Retrieves WSL distribution metadata from the typed WorkspaceBridge
        instance-list operation.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Name,
        [switch]$ForceUpdate,
        [switch]$IncludeRelease,
        [switch]$IncludeUser,
        [switch]$SkipDiskSize
    )

    process {
        $instances = @(Invoke-DistroNexusWorkspaceBridge -Operation 'instance.list.v1' -Payload @{ IncludeRelease = [bool]$IncludeRelease; IncludeUser = [bool]$IncludeUser; SkipDiskSize = [bool]$SkipDiskSize; ForceRefresh = [bool]$ForceUpdate })
        foreach ($instance in $instances) {
            if ($Name -and $instance.Name -notlike $Name) { continue }
            $result = [PSCustomObject]@{
                PSTypeName = 'DistroNexus.WslInstance'
                Name       = $instance.Name
                State      = $instance.State
                Version    = $instance.Version
                BasePath   = $instance.BasePath
                DiskSize   = $instance.DiskSize
                InstallTime = $instance.InstallTime
                Distribution = $instance.Distribution
                Guid = $instance.Guid
            }
            if ($IncludeRelease) { $result | Add-Member -NotePropertyName Release -NotePropertyValue $instance.Release }
            if ($IncludeUser) { $result | Add-Member -NotePropertyName CurrentUser -NotePropertyValue $instance.CurrentUser }
            $result
        }
    }
}
