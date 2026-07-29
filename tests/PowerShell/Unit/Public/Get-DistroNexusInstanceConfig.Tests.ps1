# Get-DistroNexusInstanceConfig.Tests.ps1 / Set-DistroNexusInstanceSparseMode.Tests.ps1
# Unit tests for E-03 Instance-Level Resource Configuration

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData = $env:APPDATA
    $script:originalProfile = $env:USERPROFILE
}

AfterAll {
    $env:APPDATA      = $script:originalAppData
    $env:USERPROFILE  = $script:originalProfile
}

Describe "Get-DistroNexusInstanceConfig" -Tag 'Unit', 'Public', 'InstanceConfig' {

    BeforeEach {
        $env:APPDATA     = $TestDrive
        $env:USERPROFILE = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Command structure" {
        It "Should be exported" {
            Get-Command Get-DistroNexusInstanceConfig | Should -Not -BeNullOrEmpty
        }

        It "Should require -Name parameter" {
            { Get-DistroNexusInstanceConfig } | Should -Throw
        }
    }

    Context "When instance not found" {
        It "Uses the Core resource route" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstanceResources { [pscustomobject]@{ Name='NonExistent'; WslVersion=2; SparseMode=$false } } -ModuleName DistroNexus
                (Get-DistroNexusInstanceConfig -Name "NonExistent").Name | Should -Be 'NonExistent'
                Assert-MockCalled Get-DistroNexusInstanceResources -Times 1 -Exactly -ModuleName DistroNexus
            }
        }
    }

    Context "When instance exists" {
        It "Should return the path-free resource snapshot" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstanceResources { [pscustomobject]@{ Name='Ubuntu-22.04'; WslVersion=2; SparseMode=$false } } -ModuleName DistroNexus

                $result = Get-DistroNexusInstanceConfig -Name "Ubuntu-22.04"
                $result | Should -Not -BeNullOrEmpty
                $result.Name | Should -Be "Ubuntu-22.04"
                $result.PSObject.Properties.Name | Should -Contain "SparseMode"
                $result.PSObject.Properties.Name | Should -Contain "WslVersion"
                $result.PSObject.Properties.Name | Should -Not -Contain "GlobalMemory"
            }
        }
    }
}

Describe "Set-DistroNexusInstanceSparseMode" -Tag 'Unit', 'Public', 'InstanceConfig' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Command structure" {
        It "Should be exported" {
            Get-Command Set-DistroNexusInstanceSparseMode | Should -Not -BeNullOrEmpty
        }

        It "Should require a Core-issued preview token" {
            { Set-DistroNexusInstanceSparseMode } | Should -Throw
        }
    }

    It "Should reject a non-token mutation request" {
        { Set-DistroNexusInstanceSparseMode -PreviewToken 'bad' -Confirm:$false } | Should -Throw
    }
}
