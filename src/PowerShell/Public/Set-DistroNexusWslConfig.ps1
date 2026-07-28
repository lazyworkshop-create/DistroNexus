function Set-DistroNexusWslConfig {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='Medium')]
    param(
        [string]$Memory,
        [ValidateRange(1, [int]::MaxValue)][int]$Processors,
        [string]$Swap,
        [nullable[bool]]$LocalhostForwarding,
        [ValidateSet('nat','mirrored','virtioproxy','none','NAT','Mirrored','VirtioProxy','None')][string]$NetworkingMode
    )
    $changes = @{}
    if ($PSBoundParameters.ContainsKey('Memory')) { $changes['wsl2.memory'] = $Memory }
    if ($PSBoundParameters.ContainsKey('Processors')) { $changes['wsl2.processors'] = [string]$Processors }
    if ($PSBoundParameters.ContainsKey('Swap')) { $changes['wsl2.swap'] = $Swap }
    if ($PSBoundParameters.ContainsKey('LocalhostForwarding')) { $changes['wsl2.localhostForwarding'] = $LocalhostForwarding.ToString().ToLowerInvariant() }
    if ($PSBoundParameters.ContainsKey('NetworkingMode')) { $changes['wsl2.networkingMode'] = $NetworkingMode.ToLowerInvariant() }
    if ($changes.Count -eq 0) { return }
    if (-not $PSCmdlet.ShouldProcess('reviewed global WSL configuration', 'Apply legacy modeled changes')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
    $preview = Get-DistroNexusGlobalConfigurationPreview -Changes $changes
    Set-DistroNexusGlobalConfiguration -PreviewToken $preview.PreviewToken -Confirm:$false
}
