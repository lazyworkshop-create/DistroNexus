# Add-DistroNexusInstanceTag.Tests.ps1
# Unit tests for E-06 Instance Tagging — Add a single tag

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

Describe "Add-DistroNexusInstanceTag" -Tag 'Unit', 'Public', 'Tags' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Add-DistroNexusInstanceTag -Tag "dev" } | Should -Throw
        }

        It "Should require -Tag parameter" {
            { Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" } | Should -Throw
        }
    }

    Context "Adding a tag" {
        It "Should add tag to an instance that has no tags" {
            InModuleScope DistroNexus {
                Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "dev"

                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                $result.Tags | Should -Contain "dev"
            }
        }

        It "Should add tag to instance that already has tags" {
            InModuleScope DistroNexus {
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("dev")
                Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "docker"

                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                $result.Tags | Should -Contain "dev"
                $result.Tags | Should -Contain "docker"
            }
        }

        It "Should normalise tag to lowercase" {
            InModuleScope DistroNexus {
                Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "PROD"

                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                $result.Tags | Should -Contain "prod"
            }
        }

        It "Should not add duplicate tag" {
            InModuleScope DistroNexus {
                Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "dev"
                Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "dev"

                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                ($result.Tags | Where-Object { $_ -eq "dev" }).Count | Should -Be 1
            }
        }

        It "Should error when 10 tags already exist" {
            InModuleScope DistroNexus {
                $tenTags = 1..10 | ForEach-Object { "tag$_" }
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags $tenTags

                $errorRecord = $null
                Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "overflow" `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue

                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }

        It "rejects a tag longer than 32 characters" {
            $longTag = "a" * 33
            { Add-DistroNexusInstanceTag -Name "Ubuntu" -Tag $longTag } | Should -Throw
        }
    }
}
