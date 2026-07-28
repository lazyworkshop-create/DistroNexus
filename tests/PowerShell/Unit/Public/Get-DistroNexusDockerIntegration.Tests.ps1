BeforeAll { Import-Module (Join-Path (Resolve-Path "$PSScriptRoot/../../../..") 'src/PowerShell/DistroNexus.psd1') -Force }

Describe 'Docker integration module contract' -Tag 'Unit','Public','Docker' {
    It 'returns the path-free fixed get projection' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ IsAvailable=$true; IsEligible=$true; Status='Enabled'; Reason=$null; Version='4.0'; RestartGuidance='restart' } }
            $result = Get-DistroNexusDockerIntegration -Name Ubuntu
            $result.Status | Should -Be Enabled; $result.PSObject.Properties.Name | Should -Not -Contain 'SettingsPath'
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'docker.integration.get.v1' -and $Payload.Name -eq 'Ubuntu' }
        }
    }
    It 'rejects invalid names before a bridge route' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {}
            { Get-DistroNexusDockerIntegration -Name "bad`nname" } | Should -Throw
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
    It 'uses exact preview then set routes through the enable facade without filesystem writes' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { if($Operation -eq 'docker.integration.preview-set.v1'){[pscustomobject]@{Token=('a'*64);Effects=@('enable')}} else {[pscustomobject]@{Succeeded=$true;OutcomeCode='DockerIntegration.Updated';RestartRequired=$true} } }
            Mock Set-Content {}; Mock Add-Content {}; Mock New-Item {}
            Enable-DistroNexusDockerIntegration -Name Ubuntu -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'docker.integration.preview-set.v1' -and $Payload.Name -eq 'Ubuntu' -and $Payload.Enabled }
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'docker.integration.set.v1' -and $Token -eq ('a'*64) -and $Payload.Enabled }
            Assert-MockCalled Set-Content -Times 0; Assert-MockCalled Add-Content -Times 0; Assert-MockCalled New-Item -Times 0
        }
    }
    It 'WhatIf requests a preview but never executes set' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{Token=('a'*64);Effects=@()} }
            Enable-DistroNexusDockerIntegration -Name Ubuntu -WhatIf | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'docker.integration.preview-set.v1' }
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -ParameterFilter { $Operation -eq 'docker.integration.set.v1' }
        }
    }
    It 'uses disabled exact payload through the disable facade' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { if($Operation -eq 'docker.integration.preview-set.v1'){[pscustomobject]@{Token=('b'*64);Effects=@()}} else {[pscustomobject]@{Succeeded=$true}} }
            Disable-DistroNexusDockerIntegration -Name Ubuntu -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'docker.integration.preview-set.v1' -and -not $Payload.Enabled }
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'docker.integration.set.v1' -and -not $Payload.Enabled }
        }
    }
}
