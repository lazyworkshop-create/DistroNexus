# Public contract tests for the fixed backup schedule read route.
BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Get-DistroNexusBackupSchedule fixed route' -Tag 'Unit', 'Public', 'Backup' {
    It 'uses only the versioned schedule list route' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                @([pscustomobject]@{ InstanceName='Ubuntu'; Frequency='Daily'; RetentionCount=3; Time='02:00:00'; Enabled=$true })
            }
            $result = @(Get-DistroNexusBackupSchedule)
            $result.Count | Should -Be 1
            $result[0].InstanceName | Should -Be 'Ubuntu'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.schedule.list.v1' -and $null -eq $Payload } -Times 1 -Exactly
        }
    }

    It 'filters the path-free result locally without a second route' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                @([pscustomobject]@{ InstanceName='Ubuntu' }, [pscustomobject]@{ InstanceName='Debian' })
            }
            @(Get-DistroNexusBackupSchedule -Name Ubuntu).InstanceName | Should -Be 'Ubuntu'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.schedule.list.v1' } -Times 1 -Exactly
        }
    }

    It 'serializes the fixed result only when AsJson is requested' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { @([pscustomobject]@{ InstanceName='Ubuntu'; Frequency='Daily' }) }
            (Get-DistroNexusBackupSchedule -AsJson | ConvertFrom-Json)[0].InstanceName | Should -Be 'Ubuntu'
        }
    }
}
