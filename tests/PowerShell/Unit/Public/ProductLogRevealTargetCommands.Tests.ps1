BeforeAll { Import-Module (Join-Path (Resolve-Path "$PSScriptRoot/../../../..") 'src/PowerShell/DistroNexus.psd1') -Force }
Describe 'Product log reveal target contract' -Tag 'Unit','Public' {
    It 'uses only the fixed empty payload' { InModuleScope DistroNexus { Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ RevealUri='file:///C:/Logs/'; OutcomeCode='ProductLog.Ready' } }; Get-DistroNexusProductLogRevealTarget -Confirm:$false | Out-Null; Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'product.log.reveal-target.v1' -and $Payload.Count -eq 0 } } }
    It 'WhatIf does not invoke the bridge and returns declined' { InModuleScope DistroNexus { Mock Invoke-DistroNexusWorkspaceBridge {}; $result=Get-DistroNexusProductLogRevealTarget -WhatIf; $result.OutcomeCode | Should -Be 'ProductLog.Declined'; Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 } }
}
