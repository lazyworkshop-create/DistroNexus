function Get-DistroNexusStoreComplianceStatus {
    [CmdletBinding()]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation 'store-compliance.get.v1'
}
