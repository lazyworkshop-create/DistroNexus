function Get-DistroNexusTemplateImportFilePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1, 2048)][string]$SourcePath)
    if ($SourcePath.IndexOf([char]0) -ge 0 -or $SourcePath -match '[\x00-\x1F]') { throw 'SourcePath contains a control character.' }
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.local.import-file-preview.v1' -Payload @{ SourcePath = $SourcePath }
}
