# Get-DistroNexusInstanceTag.Tests.ps1
# Unit tests for E-06 Instance Tagging

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

Describe "Get-DistroNexusInstanceTag" -Tag 'Unit', 'Public', 'Tags' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null

        $settingsFile = Join-Path $env:APPDATA "DistroNexus\settings.json"
        @{
            instanceTags = @{
                "Ubuntu-22.04" = @("dev", "docker")
                "Debian"       = @("prod")
            }
        } | ConvertTo-Json -Depth 5 | Set-Content $settingsFile
    }

    Context "Parameter validation" {
        It "Should have optional -Name parameter" {
            (Get-Command Get-DistroNexusInstanceTag).Parameters.ContainsKey('Name') | Should -Be $true
        }
    }

    Context "Getting all tags" {
        It "Should return tags for all instances when no Name specified" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusInstanceTag
                $result | Should -Not -BeNullOrEmpty
            }
        }
    }

    Context "Getting tags for a specific instance" {
        It "Should return tags for the specified instance" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
                $result | Should -Not -BeNullOrEmpty
                $result.Tags -contains "dev"    | Should -Be $true
                $result.Tags -contains "docker" | Should -Be $true
            }
        }

        It "Should return empty tags for instance with no tags" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusInstanceTag -Name "NotTagged"
                if ($result) {
                    $result.Tags | Should -BeNullOrEmpty
                }
                $true | Should -Be $true
            }
        }
    }

    Context "When settings file does not exist" {
        It "Should return empty without error" {
            InModuleScope DistroNexus {
                $settingsFile = Join-Path $env:APPDATA "DistroNexus\settings.json"
                Remove-Item $settingsFile -Force -ErrorAction SilentlyContinue

                $result = Get-DistroNexusInstanceTag
                $result | Should -BeNullOrEmpty
            }
        }
    }
}
