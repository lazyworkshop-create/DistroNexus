# Set-DistroNexusInstanceTag.Tests.ps1
# Unit tests for E-06 Instance Tagging — Set (replace all tags)

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

Describe "Set-DistroNexusInstanceTag" -Tag 'Unit', 'Public', 'Tags' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Set-DistroNexusInstanceTag -Tags @("dev") } | Should -Throw
        }

        It "Should require -Tags parameter" {
            { Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" } | Should -Throw
        }
    }

    Context "Setting tags" {
        It "Should persist tags to settings.json" {
            InModuleScope DistroNexus {
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("dev", "docker")

                $settingsFile = Join-Path $env:APPDATA "DistroNexus\settings.json"
                $settings = Get-Content $settingsFile | ConvertFrom-Json
                $tags = $settings.instanceTags."Ubuntu-22.04"
                $tags | Should -Contain "dev"
                $tags | Should -Contain "docker"
            }
        }

        It "Should normalise tags to lowercase" {
            InModuleScope DistroNexus {
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("DEV", "Docker")

                $settingsFile = Join-Path $env:APPDATA "DistroNexus\settings.json"
                $settings = Get-Content $settingsFile | ConvertFrom-Json
                $tags = $settings.instanceTags."Ubuntu-22.04"
                $tags | Should -Contain "dev"
                $tags | Should -Contain "docker"
            }
        }

        It "Should replace existing tags when called again" {
            InModuleScope DistroNexus {
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("dev", "docker")
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("prod")

                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                $result.Tags | Should -Contain "prod"
                $result.Tags | Should -Not -Contain "dev"
            }
        }

        It "Should enforce max 10 tags" {
            InModuleScope DistroNexus {
                $tooMany = 1..11 | ForEach-Object { "tag$_" }
                $errorRecord = $null
                Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags $tooMany `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue

                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }
}
