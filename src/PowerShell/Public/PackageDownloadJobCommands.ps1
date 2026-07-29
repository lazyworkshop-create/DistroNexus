function Start-DistroNexusPackageDownload {
    [CmdletBinding(DefaultParameterSetName='Preview', SupportsShouldProcess=$true)] param(
        [Parameter(Mandatory, ParameterSetName='Preview')][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')][string]$PackageId,
        [Parameter(Mandatory, ParameterSetName='Preview')][switch]$Preview,
        [Parameter(Mandatory, ParameterSetName='Execute')][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if ($PSCmdlet.ParameterSetName -eq 'Preview') { return Invoke-DistroNexusWorkspaceBridge -Operation 'package.jobs.start.preview.v1' -Payload @{ PackageId=$PackageId } }
    if ($PSCmdlet.ShouldProcess('reviewed package download', 'Start package download')) { Invoke-DistroNexusWorkspaceBridge -Operation 'package.jobs.start.execute.v1' -Payload @{ PreviewToken=$PreviewToken } }
}
function Get-DistroNexusPackageDownloadJob { [CmdletBinding()] param() Invoke-DistroNexusWorkspaceBridge -Operation 'package.jobs.list.v1' }
function Invoke-DistroNexusPackageDownloadJobAction {
    [CmdletBinding(DefaultParameterSetName='Preview', SupportsShouldProcess=$true)] param(
        [Parameter(Mandatory, ParameterSetName='Preview')][ValidatePattern('^[A-Fa-f0-9]{32}$')][string]$JobId,
        [Parameter(Mandatory)][ValidateSet('cancel','retry','clear')][string]$Action,
        [Parameter(Mandatory, ParameterSetName='Preview')][switch]$Preview,
        [Parameter(Mandatory, ParameterSetName='Execute')][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if ($PSCmdlet.ParameterSetName -eq 'Preview') { return Invoke-DistroNexusWorkspaceBridge -Operation "package.jobs.$Action.preview.v1" -Payload @{ JobId=$JobId } }
    if ($PSCmdlet.ShouldProcess('reviewed package download job', 'Execute package job action')) { Invoke-DistroNexusWorkspaceBridge -Operation "package.jobs.$Action.execute.v1" -Payload @{ PreviewToken=$PreviewToken } }
}
