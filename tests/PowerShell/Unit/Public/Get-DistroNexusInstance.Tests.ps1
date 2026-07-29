BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath "src\PowerShell\DistroNexus.psd1") -Force
}

Describe 'Get-DistroNexusInstance bridge mapping' -Tag 'Unit', 'Public', 'Instance' {
    It 'maps the fixed list operation to WslInstance output and filters by name' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                @(
                    [PSCustomObject]@{ Name = 'Ubuntu'; State = 'Running'; Version = 2 },
                    [PSCustomObject]@{ Name = 'Debian'; State = 'Stopped'; Version = 2 }
                )
            } -ParameterFilter { $Operation -eq 'instance.list.v1' }

            $result = @(Get-DistroNexusInstance -Name 'Ubuntu*')

            $result | Should -HaveCount 1
            $result[0].PSTypeNames | Should -Contain 'DistroNexus.WslInstance'
            $result[0].Name | Should -Be 'Ubuntu'
            $result[0].State | Should -Be 'Running'
            $result[0].Version | Should -Be 2
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.list.v1' }
        }
    }

    It 'maps list detail switches and metadata through the fixed list operation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                [PSCustomObject]@{ Name = 'Ubuntu'; State = 'Running'; Version = 2; BasePath = 'C:\\WSL\\Ubuntu'; DiskSize = 42; InstallTime = [datetime]'2024-01-01'; Distribution = 'Ubuntu'; Guid = 'test-guid'; Release = 'Ubuntu 24.04'; CurrentUser = 'dev' }
            } -ParameterFilter { $Operation -eq 'instance.list.v1' }

            $result = Get-DistroNexusInstance -IncludeRelease -IncludeUser

            $result.BasePath | Should -Be 'C:\\WSL\\Ubuntu'
            $result.DiskSize | Should -Be 42
            $result.Release | Should -Be 'Ubuntu 24.04'
            $result.CurrentUser | Should -Be 'dev'
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.list.v1' -and $Payload.IncludeRelease -and $Payload.IncludeUser }
        }
    }
}
