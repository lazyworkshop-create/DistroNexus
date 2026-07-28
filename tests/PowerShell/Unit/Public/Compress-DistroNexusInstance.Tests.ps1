BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath "src/PowerShell/DistroNexus.psd1") -Force
}

Describe "Compress-DistroNexusInstance" -Tag 'Unit', 'Public', 'Compact' {
    It "uses only the fixed preview and token-only execute bridge operations" {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                if ($Operation -eq 'instance.compact.preview.v1') { return [PSCustomObject]@{ PreviewToken = ('a' * 64) } }
                return [PSCustomObject]@{ Succeeded = $true; InstanceName = 'Ubuntu'; OutcomeCode = 'Lifecycle.Compacted'; BeforeBytes = 100; AfterBytes = 50; SavedBytes = 50; Method = 'Diskpart'; Restarted = $false; RecoveryAction = 'None' }
            }

            $result = Compress-DistroNexusInstance -Name Ubuntu -Confirm:$false

            $result.SavedBytes | Should -Be 50
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'instance.compact.preview.v1' -and $Payload.Count -eq 1 -and $Payload.Name -eq 'Ubuntu' }
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'instance.compact.execute.v1' -and $Payload.Count -eq 1 -and $Payload.PreviewToken -match '^[a-f0-9]{64}$' }
        }
    }

    It "does not create a preview token or execute work under WhatIf" {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }

            $result = Compress-DistroNexusInstance -Name Ubuntu -WhatIf

            $result.OutcomeCode | Should -Be 'WhatIf'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }

    It "keeps Force compatible without bypassing WhatIf" {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
            $result = Compress-DistroNexusInstance -Name Ubuntu -Force -WhatIf
            $result.OutcomeCode | Should -Be 'WhatIf'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
}
