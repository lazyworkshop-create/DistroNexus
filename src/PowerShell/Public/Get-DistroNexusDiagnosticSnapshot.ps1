function Get-DistroNexusDiagnosticSnapshot {
    [CmdletBinding()]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation 'diagnostic.snapshot.v1'
}
