# Test-DistroNexusTemplateEnvironment.Tests.ps1

Describe "Test-DistroNexusTemplateEnvironment" -Tag 'Unit', 'Public' {

    BeforeAll {
        $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
        $modulePath = Join-Path $rootPath "src\PowerShell"
        Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    }

    Context "Capability contract" {
        It "Should return machine-readable fields for requested capability" {
            InModuleScope DistroNexus {
                Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                Mock wsl.exe {
                    $global:LASTEXITCODE = 0
                    return 'ok'
                } -ModuleName DistroNexus

                $result = @(Test-DistroNexusTemplateEnvironment -Distro 'Ubuntu-22.04' -Capability Systemd)

                $result.Count | Should -BeGreaterThan 0
                ($result[0].PSObject.Properties.Name -contains 'Capability') | Should -BeTrue
                ($result[0].PSObject.Properties.Name -contains 'Status') | Should -BeTrue
                ($result[0].PSObject.Properties.Name -contains 'Reason') | Should -BeTrue
                ($result[0].PSObject.Properties.Name -contains 'Details') | Should -BeTrue
            }
        }

        It "Should return fail/blocked results when wsl.exe is unavailable" {
            InModuleScope DistroNexus {
                Mock Get-Command { return $null } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }

                $result = @(Test-DistroNexusTemplateEnvironment -Capability All)

                ($result | Where-Object { $_.Capability -eq 'Wsl' }).Status | Should -Be 'Fail'
                ($result | Where-Object { $_.Capability -eq 'Systemd' }).Status | Should -Be 'Blocked'
            }
        }
    }
}