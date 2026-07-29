function New-DistroNexusWorkspaceShortcut {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][Guid]$WorkspaceId)

    if ($PSCmdlet.ShouldProcess('validated workspace', 'Create DistroNexus desktop shortcut')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'workspace.shortcut.create.v1' -Payload @{ WorkspaceId = $WorkspaceId }
    } else {
        [pscustomobject]@{ OutcomeCode = 'Workspace.ShortcutDeclined' }
    }
}
