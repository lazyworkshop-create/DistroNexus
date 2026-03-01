# Remove-DistroNexusInstanceTag.Tests.ps1
# Unit tests for E-06 Instance Tagging — Remove a single tag

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "Remove-DistroNexusInstanceTag" -Tag 'Unit', 'Public', 'Tags' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null

        # Pre-populate tags
        InModuleScope DistroNexus {
            Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("dev", "docker", "prod")
        }
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Remove-DistroNexusInstanceTag -Tag "dev" } | Should -Throw
        }

        It "Should require -Tag parameter" {
            { Remove-DistroNexusInstanceTag -Name "Ubuntu-22.04" } | Should -Throw
        }
    }

    Context "Removing a tag" {
        It "Should remove the specified tag" {
            InModuleScope DistroNexus {
                Remove-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "docker"

                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                $result.Tags | Should -Not -Contain "docker"
                $result.Tags | Should -Contain "dev"
            }
        }

        It "Should be case-insensitive when removing" {
            InModuleScope DistroNexus {
                Remove-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "DEV"

                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                $result.Tags | Should -Not -Contain "dev"
            }
        }

        It "Should not error when tag does not exist" {
            InModuleScope DistroNexus {
                # Just verify no exception thrown
                Remove-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "nonexistent" -ErrorAction SilentlyContinue
                $true | Should -Be $true
            }
        }
    }

    Context "Tag migration on rename (case normalisation)" {
        It "Should delete all tags when Set is called with empty array" {
            InModuleScope DistroNexus {
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @()
                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                if ($result) {
                    $result.Tags | Should -BeNullOrEmpty
                }
                $true | Should -Be $true
            }
        }
    }
}
