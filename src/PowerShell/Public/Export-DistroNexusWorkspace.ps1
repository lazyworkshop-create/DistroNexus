function Export-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][Guid]$Id,[Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][Int64]$ExpectedRevision)
    if ($WhatIfPreference) {
        return Invoke-DistroNexusWorkspaceBridge -Operation previewExportDryRun -Id $Id -ExpectedRevision $ExpectedRevision
    }
    if ($PSCmdlet.ShouldProcess($Path, "Export workspace $Id")) {
        $content = Invoke-DistroNexusWorkspaceBridge -Operation export -Id $Id -ExpectedRevision $ExpectedRevision
        Set-Content -LiteralPath $Path -Value $content -Encoding utf8 -NoNewline
    }
}
