# Install-DistroNexusInstance.Tests.ps1
# Unit tests for Install-DistroNexusInstance Public Cmdlet (catalog name/default-name mapping)

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
}

Describe "Install-DistroNexusInstance" -Tag 'Unit', 'Public', 'Install' {
    Context "When AutoDownload is enabled and DistroName is display name" {
        It "Uses catalog DefaultName when calling Save-DistroNexusPackage" {
            InModuleScope DistroNexus {
                Mock Initialize-DistroNexusLogger { } -ModuleName DistroNexus
                Mock Write-DistroNexusLog { } -ModuleName DistroNexus
                Mock Get-DistroNexusInstance { @() } -ModuleName DistroNexus
                Mock Get-DistroNexusConfig {
                    [PSCustomObject]@{
                        Distros = @(
                            [PSCustomObject]@{
                                Name = 'Ubuntu 24.04 LTS'
                                Version = '24.04'
                                DefaultName = 'ubuntu-2404-lts'
                                LocalPath = $null
                            }
                        )
                    }
                } -ModuleName DistroNexus

                Mock Get-DistroNexusPackage { @() } -ModuleName DistroNexus
                Mock Save-DistroNexusPackage {
                    [PSCustomObject]@{
                        Success = $true
                        TotalPackages = 1
                        Downloaded = 1
                        Skipped = 0
                        Failed = 0
                        FailedPackages = @()
                    }
                } -ModuleName DistroNexus -ParameterFilter { $DefaultName -eq 'ubuntu-2404-lts' }

                $result = Install-DistroNexusInstance -DistroName 'Ubuntu 24.04 LTS' -InstallPath 'D:\wsl' -InstanceName 'test-instance' -AutoDownload

                $result | Should -BeFalse
                Should -Invoke Save-DistroNexusPackage -ModuleName DistroNexus -Times 1 -Exactly -ParameterFilter { $DefaultName -eq 'ubuntu-2404-lts' }
            }
        }
    }

    Context "When AutoDownload summary reports failure" {
        It "Returns false and does not continue to import package" {
            InModuleScope DistroNexus {
                Mock Initialize-DistroNexusLogger { } -ModuleName DistroNexus
                Mock Write-DistroNexusLog { } -ModuleName DistroNexus
                Mock Get-DistroNexusInstance { @() } -ModuleName DistroNexus
                Mock Get-DistroNexusConfig {
                    [PSCustomObject]@{
                        Distros = @(
                            [PSCustomObject]@{
                                Name = 'Ubuntu 24.04 LTS'
                                Version = '24.04'
                                DefaultName = 'Ubuntu-24.04'
                                LocalPath = $null
                            }
                        )
                    }
                } -ModuleName DistroNexus

                Mock Get-DistroNexusPackage { @() } -ModuleName DistroNexus

                Mock Save-DistroNexusPackage {
                    [PSCustomObject]@{
                        Success = $false
                        TotalPackages = 1
                        Downloaded = 0
                        Skipped = 0
                        Failed = 1
                        FailedPackages = @('Ubuntu-24.04')
                    }
                } -ModuleName DistroNexus

                $result = Install-DistroNexusInstance -DistroName 'Ubuntu 24.04 LTS' -InstallPath 'D:\wsl' -InstanceName 'test-instance' -AutoDownload

                $result | Should -BeFalse
                Should -Invoke Save-DistroNexusPackage -ModuleName DistroNexus -Times 1 -Exactly
                Should -Invoke Get-DistroNexusPackage -ModuleName DistroNexus -Times 1 -Exactly
            }
        }
    }
}
