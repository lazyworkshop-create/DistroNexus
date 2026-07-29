# Public contract tests for the constrained schedule facade.
BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'New-DistroNexusBackupSchedule fixed route' -Tag 'Unit', 'Public', 'Backup' {
    It 'retains Destination only as an optional preview field and executes only PreviewToken' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                if ($Operation -eq 'backup.schedule.preview.v1') { return [pscustomobject]@{ Token=('a' * 32) } }
                [pscustomobject]@{ Succeeded=$true; OutcomeCode='Scheduled' }
            }
            New-DistroNexusBackupSchedule -Name Ubuntu -Destination 'C:\Backups' -Frequency Daily -RetentionCount 3 -Time ([TimeSpan]'02:00:00') -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.schedule.preview.v1' -and $Payload.InstanceName -eq 'Ubuntu' -and $Payload.Destination -eq 'C:\Backups' } -Times 1 -Exactly
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.execute.v1' -and @($Payload.Keys) -eq @('PreviewToken') -and $Payload.PreviewToken -eq ('a' * 32) } -Times 1 -Exactly
        }
    }

    It 'allows a current typed client to omit the legacy destination' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                if ($Operation -eq 'backup.schedule.preview.v1') { return [pscustomobject]@{ Token=('b' * 32) } }
                [pscustomobject]@{ Succeeded=$true }
            }
            New-DistroNexusBackupSchedule -Name Ubuntu -Frequency Daily -RetentionCount 3 -Time ([TimeSpan]'02:00:00') -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.schedule.preview.v1' -and -not $Payload.ContainsKey('Destination') } -Times 1 -Exactly
        }
    }

    It 'does not issue a grant or mutate under WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
            $result = New-DistroNexusBackupSchedule -Name Ubuntu -Frequency Daily -RetentionCount 3 -Time ([TimeSpan]'02:00:00') -WhatIf
            $result.OutcomeCode | Should -Be 'WhatIf'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
}
