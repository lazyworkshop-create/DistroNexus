Describe 'Path-bearing export and import facades' -Tag 'Unit', 'Public', 'Lifecycle' {
    BeforeAll { Import-Module (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1') -Force }
    It 'exports through fixed preview and token-only execute routes' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { if ($Operation -eq 'instance.export.preview.v1') { [pscustomobject]@{ PreviewToken = ('a' * 64) } } else { [pscustomobject]@{ Succeeded = $true; Operation = 'Export'; InstanceName = 'Ubuntu'; OutcomeCode = 'Lifecycle.Succeeded' } } }
            $result = Export-DistroNexusInstance -Name Ubuntu -Destination C:\exports\ubuntu.tar -Confirm:$false
            $result.Succeeded | Should -BeTrue
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'instance.export.preview.v1' -and $Payload.Destination -eq 'C:\exports\ubuntu.tar' } -Times 1
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'instance.export.execute.v1' -and $Payload.Keys.Count -eq 1 -and $Payload.PreviewToken -eq ('a' * 64) } -Times 1
        }
    }
    It 'does not issue an export preview under WhatIf' {
        InModuleScope DistroNexus { Mock Invoke-DistroNexusWorkspaceBridge {}; Export-DistroNexusInstance -Name Ubuntu -Destination C:\exports\ubuntu.tar -WhatIf | Should -Not -BeNullOrEmpty; Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 }
    }
    It 'maps StopRunning without allowing Force to change the lifecycle payload' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { if ($Operation -eq 'instance.export.preview.v1') { [pscustomobject]@{ PreviewToken = ('c' * 64) } } else { [pscustomobject]@{ Succeeded = $true } } }
            Export-DistroNexusInstance -Name Ubuntu -Destination C:\exports\ubuntu.tar -Force -Confirm:$false | Out-Null
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'instance.export.preview.v1' -and $Payload.StopRunning -eq $false } -Times 1
            Export-DistroNexusInstance -Name Ubuntu -Destination C:\exports\ubuntu.tar -StopRunning -Confirm:$false | Out-Null
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'instance.export.preview.v1' -and $Payload.StopRunning -eq $true } -Times 1
        }
    }
    It 'imports through fixed preview and token-only execute routes' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { if ($Operation -eq 'instance.import.preview.v1') { [pscustomobject]@{ PreviewToken = ('b' * 64) } } else { [pscustomobject]@{ Succeeded = $true; Operation = 'Import'; InstanceName = 'Ubuntu'; OutcomeCode = 'Lifecycle.Succeeded' } } }
            $result = Import-DistroNexusInstance -Name Ubuntu -Source C:\exports\ubuntu.tar -InstallPath C:\wsl -Confirm:$false
            $result.Succeeded | Should -BeTrue
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'instance.import.preview.v1' -and $Payload.Source -eq 'C:\exports\ubuntu.tar' -and $Payload.InstallPath -eq 'C:\wsl' } -Times 1
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -ParameterFilter { $Operation -eq 'instance.import.execute.v1' -and $Payload.Keys.Count -eq 1 -and $Payload.PreviewToken -eq ('b' * 64) } -Times 1
        }
    }
}
