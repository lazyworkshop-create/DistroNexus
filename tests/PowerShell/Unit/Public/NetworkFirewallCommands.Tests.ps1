BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}
Describe 'Network and firewall command consent' -Tag 'Unit', 'Public', 'Network', 'Firewall' {
    It 'does not invoke the network mode route under WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
            Set-DistroNexusNetworkMode -Mode Nat -PreviewToken '0123456789abcdef' -WhatIf
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
    It 'does not invoke firewall creation under WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
            New-DistroNexusFirewallRule -PreviewRuleId 'DistroNexus-0123456789ABCDEF' -WhatIf
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
    It 'maps firewall create only to its fixed route with its reviewed preview' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded = $false; OutcomeCode = 'ElevatedHelperUnavailable' } }
            New-DistroNexusFirewallRule -PreviewRuleId 'DistroNexus-0123456789ABCDEF' -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'firewall.create.v1' -and $Payload.PreviewRuleId -eq 'DistroNexus-0123456789ABCDEF' }
        }
    }
    It 'rejects malformed public input before bridge invocation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
            { Test-DistroNexusNetworkProbe -Kind Bad -Host example.test } | Should -Throw
            { Test-DistroNexusNetworkProbe -Kind TcpEndpoint -Host example.test -Port 70000 } | Should -Throw
            { Get-DistroNexusNetworkSettingsPreview } | Should -Throw
            { Get-DistroNexusNetworkSettingsPreview -IgnoredPorts '$(Get-ChildItem)' } | Should -Throw
            { Set-DistroNexusNetworkSettings -PreviewToken '0123456789abcdef' -Settings @{ Unknown = $true } } | Should -Throw
            { New-DistroNexusFirewallRule -PreviewRuleId 'forged' } | Should -Throw
            { Remove-DistroNexusFirewallRule -PreviewToken 'short' } | Should -Throw
            { Get-DistroNexusFirewallRuleCreatePreview -Direction Inbound -Protocol Tcp -Port 443 -Profiles Private -RemoteScope '999.999.999.999/999' } | Should -Throw
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }
    }
    It 'accepts only a semantically valid firewall remote scope' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{} }
            Get-DistroNexusFirewallRuleCreatePreview -Direction Inbound -Protocol Tcp -Port 443 -Profiles Private -RemoteScope '2001:db8::/64' | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'firewall.preview-create.v1' -and $Payload.Request.RemoteScope -eq '2001:db8::/64' }
        }
    }
}
