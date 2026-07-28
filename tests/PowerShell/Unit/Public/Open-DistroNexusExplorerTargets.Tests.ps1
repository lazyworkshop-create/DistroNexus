BeforeAll {
    $script:rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Fixed Explorer command contract' -Tag 'Unit', 'Public' {
    It 'exports only the fixed WSL config and recovery point commands' {
        'Open-DistroNexusWslConfigFile', 'Open-DistroNexusRecoveryPointFolder' | ForEach-Object { Get-Command $_ -Module DistroNexus | Should -Not -BeNullOrEmpty }
    }

    It 'maps to closed bridge operations without a caller target' {
        InModuleScope DistroNexus {
            $id = [guid]::NewGuid()
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded=$true; OutcomeCode='Opened' } }
            Open-DistroNexusWslConfigFile -Confirm:$false | Out-Null
            Open-DistroNexusRecoveryPointFolder -Id $id -Confirm:$false | Out-Null
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'explorer.wslconfig.v1' -and $null -eq $Payload }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'explorer.recovery-point.v1' -and $Id -eq $id -and $null -eq $Payload }
        }
    }

    It 'does not invoke either route for WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }
            Open-DistroNexusWslConfigFile -WhatIf | Should -Not -BeNullOrEmpty
            Open-DistroNexusRecoveryPointFolder -Id ([guid]::NewGuid()) -WhatIf | Should -Not -BeNullOrEmpty
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }
}
