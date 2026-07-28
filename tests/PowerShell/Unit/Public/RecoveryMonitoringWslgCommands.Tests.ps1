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
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recovery.verify.v1' } -Times 1
    }
    It 'returns a Core deletion preview without mutation under WhatIf' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Token='delete-preview'; Operation='Delete'; RecoveryPointId='11111111-1111-1111-1111-111111111111' } }
        $preview = Remove-DistroNexusRecoveryPoint -Id '11111111-1111-1111-1111-111111111111' -WhatIf
        $preview.Token | Should -Be 'delete-preview'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recovery.preview-remove.v1' } -Times 1
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recovery.remove.v1' } -Times 0
    }
    It 'requires a Core-issued token to execute recovery deletion' {
        $preview = [pscustomobject]@{ Token='delete-preview'; Operation='Delete'; RecoveryPointId='11111111-1111-1111-1111-111111111111' }
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded=$true } }
        Remove-DistroNexusRecoveryPoint -Id '11111111-1111-1111-1111-111111111111' -Preview $preview -Confirm:$false
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'recovery.remove.v1' -and $Token -eq 'delete-preview' } -Times 1
    }
    It 'uses versioned typed recovery history and retention routes' {
        Mock Invoke-DistroNexusWorkspaceBridge {
            if ($Operation -eq 'recovery.retention.preview.v1') { return [pscustomobject]@{ Operation=$Operation; Payload=$Payload; Token='retention-preview'; SourceInstance='Ubuntu'; Maximum=3 } }
            [pscustomobject]@{ Operation=$Operation; Payload=$Payload; Id=$Id; Token=$Token }
        }
        (Get-DistroNexusRecoveryPointHistory).Operation | Should -Be 'recovery.history.v1'
        (Get-DistroNexusRecoveryPointRetention -Name Ubuntu).Operation | Should -Be 'recovery.retention.get.v1'
        $preview = Get-DistroNexusRecoveryPointRetentionPreview -Name Ubuntu -Maximum 3
        (Set-DistroNexusRecoveryPointRetention -Name Ubuntu -Maximum 3 -Preview $preview -Confirm:$false).Operation | Should -Be 'recovery.retention.set.v1'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'recovery.retention.set.v1' -and $Payload.Maximum -eq 3 -and $Token -eq $preview.Token }
    }
    It 'does not call the bridge for invalid, WhatIf, or declined retention updates' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
        { Set-DistroNexusRecoveryPointRetention -Name Ubuntu -Maximum 3 -Preview ([pscustomobject]@{}) -Confirm:$false } | Should -Throw
        Set-DistroNexusRecoveryPointRetention -Name Ubuntu -Maximum 3 -Preview ([pscustomobject]@{ Token='t'; SourceInstance='Ubuntu'; Maximum=3 }) -WhatIf | Should -Not -BeNullOrEmpty
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        $root = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
        $modulePath = Join-Path $root 'src\PowerShell\DistroNexus.psd1'
        $declined = 'N' | & pwsh -NoProfile -Command "& { Import-Module '$modulePath' -Force -DisableNameChecking; `$env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = 'invalid'; Set-DistroNexusRecoveryPointRetention -Name Ubuntu -Maximum 3 -Preview ([pscustomobject]@{ Token='retention-preview'; SourceInstance='Ubuntu'; Maximum=3 }) -Confirm }" 2>&1
        ($declined | Out-String) | Should -Not -Match 'WorkspaceBridgeUnavailable'
    }
    It 'uses fixed versioned monitoring routes and does not expose raw process authority' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ CapturedAt='2026-01-01T00:00:00Z'; CpuPercent=2; UnavailableMetrics=@{}; SnapshotToken=('a' * 64) } }
        (Get-DistroNexusMonitoringSnapshot -Name Ubuntu).CpuPercent | Should -Be 2
        Get-DistroNexusMonitoringProcessActionPreview -SnapshotToken ('a' * 64) -ProcessId 22 -Action Terminate | Out-Null
        Invoke-DistroNexusMonitoringProcessAction -PreviewToken ('b' * 64) -WhatIf | Should -BeNullOrEmpty
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'monitoring.snapshot.v1' -and $Payload.Name -eq 'Ubuntu' -and $Payload.IntervalSeconds -eq 2 } -Times 1
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'monitoring.process.preview.v1' -and $Payload.ProcessId -eq 22 -and $Payload.Action -eq 'Terminate' } -Times 1
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'monitoring.process.execute.v1' } -Times 0
        { Get-DistroNexusMonitoringProcessActionPreview -SnapshotToken bad -ProcessId 22 -Action Terminate } | Should -Throw
    }
    It 'uses discovery grants and honors WhatIf without invoking the action route' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        (Start-DistroNexusWslgApplication -DiscoveryToken ('a' * 64) -ApplicationId app -WhatIf).Detail | Should -Be 'WhatIf'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }
    It 'validates public reveal and pin grants and honors WhatIf without invoking the bridge' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        { Show-DistroNexusWslgApplicationEntry -DiscoveryToken bad -ApplicationId app } | Should -Throw
        { Set-DistroNexusWslgApplicationPin -DiscoveryToken bad -ApplicationId app -Pinned $true } | Should -Throw
        Show-DistroNexusWslgApplicationEntry -DiscoveryToken ('a' * 64) -ApplicationId app -WhatIf | Should -BeNullOrEmpty
        Set-DistroNexusWslgApplicationPin -DiscoveryToken ('a' * 64) -ApplicationId app -Pinned $true -WhatIf | Should -BeNullOrEmpty
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }
}
