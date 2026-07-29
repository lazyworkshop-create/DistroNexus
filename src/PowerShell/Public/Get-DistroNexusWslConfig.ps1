function Get-DistroNexusGlobalConfiguration {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'configuration.global.get.v1'
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 12) }
    return $value
}

function Get-DistroNexusGlobalConfigurationPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Changes)
    if ($Changes.Count -eq 0) { throw 'At least one supported global configuration change is required.' }
    $allowed = @('wsl2.memory','wsl2.processors','wsl2.swap','wsl2.swapFile','wsl2.pageReporting','wsl2.localhostForwarding','wsl2.networkingMode','wsl2.dnsTunneling','wsl2.firewall','wsl2.autoProxy','wsl2.hostAddressLoopback','wsl2.ignoredPorts','wsl2.bestEffortDnsParsing','wsl2.initialAutoProxyTimeout','wsl2.kernel','wsl2.kernelCommandLine','wsl2.nestedVirtualization','experimental.autoMemoryReclaim','experimental.sparseVhd')
    foreach ($key in $Changes.Keys) {
        $value = $Changes[$key]
        if (-not $allowed.Contains([string]$key) -or [string]$key -notmatch '^(wsl2|experimental)\.[A-Za-z]+$' -or ($null -ne $value -and ([string]$value).Length -gt 512) -or ($null -ne $value -and ([string]$value -match "[\r\n\0]"))) { throw 'Global configuration changes must use supported modeled fields and bounded values.' }
    }
    Invoke-DistroNexusWorkspaceBridge -Operation 'configuration.global.preview.v1' -Payload @{ Changes = $Changes }
}

function Set-DistroNexusGlobalConfiguration {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='Medium')]
    param([Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{32}$')][string]$PreviewToken)
    if (-not $PSCmdlet.ShouldProcess('reviewed global WSL configuration', 'Apply')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
    Invoke-DistroNexusWorkspaceBridge -Operation 'configuration.global.execute.v1' -Payload @{ PreviewToken = $PreviewToken }
}

function Get-DistroNexusWslConfig {
    [CmdletBinding()]
    param()
    $snapshot = Get-DistroNexusGlobalConfiguration
    $values = $snapshot.Values
    [pscustomobject]@{
        PSTypeName = 'DistroNexus.WslConfig'
        Memory = $values.'wsl2.memory'
        Processors = $values.'wsl2.processors'
        Swap = $values.'wsl2.swap'
        LocalhostForwarding = $values.'wsl2.localhostForwarding'
        NetworkingMode = $values.'wsl2.networkingMode'
    }
}
