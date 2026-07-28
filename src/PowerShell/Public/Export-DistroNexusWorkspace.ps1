function Export-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if ($PSCmdlet.ShouldProcess('reviewed workspace','Export workspace content')) { Invoke-DistroNexusWorkspaceBridge -Operation 'workspace.export.execute.v1' -Payload @{ PreviewToken=$PreviewToken } }
}
