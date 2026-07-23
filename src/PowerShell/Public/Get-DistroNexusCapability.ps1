function Get-DistroNexusCapability {
    <#
    .SYNOPSIS
    Returns the Core capability snapshot for the host or one WSL distribution.
    .DESCRIPTION
    This read-only command is a thin adapter over the packaged Core bridge. It does
    not infer support from the Windows build or invoke a shell command itself.
    #>
    [CmdletBinding()]
    param(
        [ValidatePattern('^[^\r\n\0]+$')]
        [string]$Name,
        [switch]$InstanceOnly,
        [switch]$AsJson
    )
    $value = Invoke-DistroNexusWorkspaceBridge -Operation capability -Payload @{ InstanceName = $Name; InstanceOnly = [bool]$InstanceOnly }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }
    $value
}
