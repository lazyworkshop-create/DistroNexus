# Public contract tests for the constrained schedule removal facade.
BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Remove-DistroNexusBackupSchedule fixed route' -Tag 'Unit', 'Public', 'Backup' {
    It 'uses the removal preview and a token-only execute payload' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                if ($Operation -eq 'backup.schedule.remove.preview.v1') { return [pscustomobject]@{ Token=('d' * 32) } }
                [pscustomobject]@{ Succeeded=$true; OutcomeCode='ScheduleRemoved' }
            }
            Remove-DistroNexusBackupSchedule -Name Ubuntu -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.schedule.remove.preview.v1' -and $Payload.InstanceName -eq 'Ubuntu' } -Times 1 -Exactly
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.execute.v1' -and @($Payload.Keys) -eq @('PreviewToken') -and $Payload.PreviewToken -eq ('d' * 32) } -Times 1 -Exactly
        }
    }

    It 'does not issue a preview or execute when confirmation is declined' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
            (Remove-DistroNexusBackupSchedule -Name Ubuntu -WhatIf).OutcomeCode | Should -Be 'WhatIf'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
}
