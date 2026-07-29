function Remove-DistroNexusPackage {
    <# .SYNOPSIS Removes one authenticated package-cache entry. #>
    [CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'ByCacheEntryId', ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory, ParameterSetName = 'ByCacheEntryId')][ValidateNotNullOrEmpty()][string]$CacheEntryId,
        [Parameter(Mandatory, ParameterSetName = 'ByDefaultName')][ValidateNotNullOrEmpty()][string]$DefaultName,
        [Parameter(Mandatory, ParameterSetName = 'ByLocalPath')][ValidateNotNullOrEmpty()][string]$LocalPath
    )

    if ($PSCmdlet.ShouldProcess('DistroNexus package cache entry', 'Remove')) {
        $payload = switch ($PSCmdlet.ParameterSetName) {
            'ByCacheEntryId' { @{ CacheEntryId = $CacheEntryId } }
            'ByDefaultName' { @{ DefaultName = $DefaultName } }
            'ByLocalPath' { @{ LocalPath = $LocalPath } }
        }
        Invoke-DistroNexusWorkspaceBridge -Operation 'package-cache.delete.v1' -Payload $payload
    }
}
