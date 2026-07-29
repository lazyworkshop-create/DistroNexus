# Public contract tests for the constrained manual backup facade.
BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Invoke-DistroNexusBackup fixed route' -Tag 'Unit', 'Public', 'Backup' {
    It 'uses the manual preview and a token-only execute payload' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                if ($Operation -eq 'backup.manual.preview.v1') { return [pscustomobject]@{ Token=('c' * 32) } }
                [pscustomobject]@{ Succeeded=$true; OutcomeCode='Completed' }
            }
            Invoke-DistroNexusBackup -Name Ubuntu -Destination 'C:\Backups' -RetentionCount 3 -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.manual.preview.v1' -and $Payload.Destination -eq 'C:\Backups' } -Times 1 -Exactly
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'backup.execute.v1' -and @($Payload.Keys) -eq @('PreviewToken') -and $Payload.PreviewToken -eq ('c' * 32) } -Times 1 -Exactly
        }
    }

    It 'rejects missing required typed fields before reaching the bridge' {
        { Invoke-DistroNexusBackup -Name Ubuntu } | Should -Throw
    }

    It 'does not issue a preview or execute under WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
            (Invoke-DistroNexusBackup -Name Ubuntu -RetentionCount 3 -WhatIf).OutcomeCode | Should -Be 'WhatIf'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
}
