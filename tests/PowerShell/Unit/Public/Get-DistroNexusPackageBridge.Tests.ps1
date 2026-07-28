Describe 'Get-DistroNexusPackage bridge contract' -Tag 'Unit', 'Public' {
    BeforeAll {
        $root = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
        Import-Module (Join-Path $root 'src\PowerShell\DistroNexus.psd1') -Force
    }

    It 'maps list, family, search and get to fixed catalog routes' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { $Payload }
            Get-DistroNexusPackage | Out-Null
            Get-DistroNexusPackage -Family Ubuntu | Out-Null
            Get-DistroNexusPackage -ForceReload | Out-Null
            Get-DistroNexusPackage -Query ubuntu | Out-Null
            Get-DistroNexusPackage -Id ubuntu-24-04 | Out-Null

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 3 -Exactly -ParameterFilter { $Operation -eq 'catalog.list.v1' }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'catalog.list.v1' -and $Payload.ForceReload -eq $true }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'catalog.search.v1' -and $Payload.Query -eq 'ubuntu' }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'catalog.get.v1' -and $Payload.Id -eq 'ubuntu-24-04' }
        }
    }
}
