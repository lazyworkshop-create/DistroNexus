Describe 'Recovery, monitoring and WSLg PowerShell bridge adapters' -Tag 'Unit', 'Public', 'Automation' {
    BeforeAll {
        function Invoke-DistroNexusWorkspaceBridge { param($Operation,$Payload,$Id,$Token) [pscustomobject]@{ Operation=$Operation; Payload=$Payload; Id=$Id; Token=$Token } }
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\RecoveryPointCommands.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusMonitoringSnapshot.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\WslgCommands.ps1')
    }
    It 'uses bridge objects for recovery verification and deletion' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Verification='Verified' } }
        (Test-DistroNexusRecoveryPoint -Id '11111111-1111-1111-1111-111111111111').Verification | Should -Be 'Verified'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recoveryVerify' } -Times 1
    }
    It 'returns a Core deletion preview without mutation under WhatIf' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Token='delete-preview'; Operation='Delete'; RecoveryPointId='11111111-1111-1111-1111-111111111111' } }
        $preview = Remove-DistroNexusRecoveryPoint -Id '11111111-1111-1111-1111-111111111111' -WhatIf
        $preview.Token | Should -Be 'delete-preview'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recoveryPreviewRemove' } -Times 1
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recoveryRemove' } -Times 0
    }
    It 'requires a Core-issued token to execute recovery deletion' {
        $preview = [pscustomobject]@{ Token='delete-preview'; Operation='Delete'; RecoveryPointId='11111111-1111-1111-1111-111111111111' }
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded=$true } }
        Remove-DistroNexusRecoveryPoint -Id '11111111-1111-1111-1111-111111111111' -Preview $preview -Confirm:$false
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recoveryRemove' -and $Token -eq 'delete-preview' } -Times 1
    }
    It 'returns a bounded Core monitoring sample object' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ CapturedAt='2026-01-01T00:00:00Z'; CpuPercent=2; UnavailableMetrics=@{} } }
        (Get-DistroNexusMonitoringSnapshot -Name Ubuntu).CpuPercent | Should -Be 2
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'monitorSnapshot' -and $Payload.InstanceName -eq 'Ubuntu' } -Times 1
    }
    It 'uses discovered WSLg objects and honors WhatIf' {
        $app = [pscustomobject]@{ Id='app'; InstanceName='Ubuntu'; Name='Editor'; Executable='/usr/bin/editor' }
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        (Start-DistroNexusWslgApplication -Application $app -WhatIf).Detail | Should -Be 'WhatIf'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }
}
