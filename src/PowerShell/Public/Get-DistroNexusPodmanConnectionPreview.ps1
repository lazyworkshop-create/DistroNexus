function Get-DistroNexusPodmanConnectionPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,62}$')][string]$ConnectionName, [Parameter(Mandatory)][uri]$Endpoint)
    if ($Endpoint.UserInfo -or $Endpoint.Query -or $Endpoint.Fragment -or (($Endpoint.Scheme -ne 'unix' -or $Endpoint.AbsolutePath -notmatch '^/run/user/.+/podman/podman\.sock$') -and (($Endpoint.Scheme -notin @('tcp','http')) -or -not $Endpoint.IsLoopback -or $Endpoint.Port -lt 1))) { throw 'Only a credential-free local Podman Unix socket or loopback TCP endpoint is permitted.' }
    Invoke-DistroNexusWorkspaceBridge -Operation previewPodmanConnection -Payload @{ InstanceName=$Name; Name=$ConnectionName; Endpoint=$Endpoint.GetComponents([UriComponents]::AbsoluteUri, [UriFormat]::UriEscaped) }
}
