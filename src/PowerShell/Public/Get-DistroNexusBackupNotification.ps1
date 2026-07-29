function Get-DistroNexusBackupNotification {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'backup.notifications.consume.v1'
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 8 -Compress) }
    return $value
}
