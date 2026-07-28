BeforeAll {
    $script:rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $script:rootPath "src\PowerShell\DistroNexus.psd1") -Force
}

Describe 'Instance lifecycle bridge routing and consent' -Tag 'Unit', 'Public', 'Instance' {
    It 'maps start to its fixed bridge operation and payload' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [PSCustomObject]@{ Succeeded = $true; Started = $true; KeepAliveEstablished = $false } } -ParameterFilter { $Operation -eq 'instance.start.v1' }

            $result = Start-DistroNexusInstance -Name Ubuntu -Confirm:$false
            $result.Succeeded | Should -BeTrue
            $result.Started | Should -BeTrue
            $result.KeepAliveEstablished | Should -BeFalse

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.start.v1' -and $Payload.Name -eq 'Ubuntu' }
        }
    }

    It 'maps stop to its fixed bridge operation and payload' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [PSCustomObject]@{ Succeeded = $true } } -ParameterFilter { $Operation -eq 'instance.stop.v1' }

            (Stop-DistroNexusInstance -Name Ubuntu -Confirm:$false).Succeeded | Should -BeTrue

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.stop.v1' -and $Payload.Name -eq 'Ubuntu' }
        }
    }

    It 'uses the fixed stop route when Force skips confirmation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [PSCustomObject]@{ Succeeded = $true } } -ParameterFilter { $Operation -eq 'instance.stop.v1' }

            (Stop-DistroNexusInstance -Name Ubuntu -Force).Succeeded | Should -BeTrue

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.stop.v1' -and $Payload.Name -eq 'Ubuntu' }
        }
    }

    It 'does not invoke the bridge for start under WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }

            Start-DistroNexusInstance -Name Ubuntu -WhatIf | Should -BeFalse

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'maps KeepAlive only through the fixed start payload' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [PSCustomObject]@{ Succeeded = $true; Started = $true; KeepAliveEstablished = $true } } -ParameterFilter { $Operation -eq 'instance.start.v1' }

            (Start-DistroNexusInstance -Name Ubuntu -KeepAlive -Confirm:$false).KeepAliveEstablished | Should -BeTrue

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.start.v1' -and $Payload.Name -eq 'Ubuntu' -and $Payload.KeepAlive }
        }
    }

    It 'does not invoke the bridge for stop under WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }

            Stop-DistroNexusInstance -Name Ubuntu -WhatIf | Should -BeFalse

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'does not invoke the bridge when start confirmation is declined' {
        $modulePath = Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1'
        $output = 'N' | & pwsh -NoProfile -Command "& { Import-Module '$modulePath' -Force -DisableNameChecking; `$env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = 'invalid'; Start-DistroNexusInstance -Name Ubuntu -Confirm }" 2>&1

        ($output | Out-String) | Should -Not -Match 'WorkspaceBridgeUnavailable'
    }

    It 'does not invoke the bridge when stop confirmation is declined' {
        $modulePath = Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1'
        $output = 'N' | & pwsh -NoProfile -Command "& { Import-Module '$modulePath' -Force -DisableNameChecking; `$env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = 'invalid'; Stop-DistroNexusInstance -Name Ubuntu -Confirm }" 2>&1

        ($output | Out-String) | Should -Not -Match 'WorkspaceBridgeUnavailable'
    }
}
