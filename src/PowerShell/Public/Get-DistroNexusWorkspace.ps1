function Get-DistroNexusWorkspace {
    [CmdletBinding()]
    param([Guid]$Id)
    $workspaces = @(Invoke-DistroNexusWorkspaceBridge -Operation list)
    if ($PSBoundParameters.ContainsKey('Id')) { return $workspaces | Where-Object { $_.Id -eq $Id } }
    $workspaces
}
