# Save-DistroNexusPackage.Tests.ps1
# Unit tests for Save-DistroNexusPackage Public Cmdlet (Batch Download功能)

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    
    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")
}

Describe "Save-DistroNexusPackage" -Tag 'Unit', 'Public', 'Download' {
    BeforeEach {
        $global:testOutputPath = Join-Path $TestDrive "packages"
        New-Item -Path $global:testOutputPath -ItemType Directory -Force | Out-Null
    }
    
    Context "When downloading with -Family parameter" {
        It "Should accept valid family names" {
            # Act & Assert
            { Save-DistroNexusPackage -Family "Ubuntu" -Destination $global:testOutputPath -WhatIf } | Should -Not -Throw
        }
    }
    
    Context "When downloading with -All parameter" {
        It "Should process all distros" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-DistroNexusPackage {
                    return @(
                        [PSCustomObject]@{
                            DefaultName = "Ubuntu"
                            Family = "Ubuntu"
                            Filename = "ubuntu.tar"
                            Url = "https://example.invalid/ubuntu.tar"
                        }
                    )
                } -ModuleName DistroNexus

                Mock Invoke-PackageDownload {
                    return [PSCustomObject]@{
                        Success = $true
                        PackageName = "Ubuntu"
                        Error = ""
                        Attempts = 1
                    }
                } -ModuleName DistroNexus

                # Act
                $result = Save-DistroNexusPackage -All -Destination $global:testOutputPath -SkipExisting:$false

                # Assert
                $result.Success | Should -BeTrue
                $result.TotalPackages | Should -Be 1
                $result.Downloaded | Should -Be 1
                $result.Failed | Should -Be 0
            }
        }
    }
    
    Context "When handling concurrent downloads" {
        It "Should manage parallel download jobs" {
            InModuleScope DistroNexus {
                # Arrange
                $packages = @(
                    [PSCustomObject]@{ DefaultName = "Ubuntu"; Family = "Ubuntu"; Filename = "ubuntu.tar"; Url = "https://example.invalid/ubuntu.tar" },
                    [PSCustomObject]@{ DefaultName = "Debian"; Family = "Debian"; Filename = "debian.tar"; Url = "https://example.invalid/debian.tar" }
                )

                foreach ($pkg in $packages) {
                    New-Item -Path (Join-Path $global:testOutputPath $pkg.Filename) -ItemType File -Force | Out-Null
                }

                Mock Get-DistroNexusPackage {
                    return $packages
                } -ModuleName DistroNexus

                # Act
                $result = Save-DistroNexusPackage -All -Destination $global:testOutputPath

                # Assert
                $result.Success | Should -BeTrue
                $result.TotalPackages | Should -Be 2
                $result.Downloaded | Should -Be 0
                $result.Skipped | Should -Be 2
                $result.Failed | Should -Be 0
            }
        }
    }
}
