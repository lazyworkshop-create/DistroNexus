# Invoke-DistroNexusTemplateAutomation.Tests.ps1

Describe "Invoke-DistroNexusTemplateAutomation" -Tag 'Unit', 'Public' {

    BeforeAll {
        $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
        $modulePath = Join-Path $rootPath "src\PowerShell"
        Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    }

    Context "Policy and selection" {
        It "Should skip in CI mode unless override is provided" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                try {
                    $env:CI = 'true'
                    $result = Invoke-DistroNexusTemplateAutomation
                    $result.Status | Should -Be 'SkippedByPolicy'
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should fail fast for unknown selected template IDs" {
            InModuleScope DistroNexus {
                Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                Mock Get-DistroNexusTemplate {
                    return @(
                        [pscustomobject]@{ Id = 'dotnet-dev'; Name = '.NET Development' },
                        [pscustomobject]@{ Id = 'nodejs-dev'; Name = 'Node.js Development' }
                    )
                } -ModuleName DistroNexus

                { Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'unknown-template' -Distro 'Ubuntu-22.04' } | Should -Throw
            }
        }
    }

    Context "Execution and artifacts" {
        It "Should generate manifest, summary and xml in dry-run mode" {
            InModuleScope DistroNexus {
                Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                Mock Get-DistroNexusTemplate {
                    return @(
                        [pscustomobject]@{ Id = 'dotnet-dev'; Name = '.NET Development'; ScenarioTags = @('api') },
                        [pscustomobject]@{ Id = 'nodejs-dev'; Name = 'Node.js Development'; ScenarioTags = @('frontend') }
                    )
                } -ModuleName DistroNexus
                Mock wsl.exe {
                    $global:LASTEXITCODE = 0
                    return 'ok'
                } -ModuleName DistroNexus
                Mock Apply-DistroNexusTemplate { } -ModuleName DistroNexus

                $outputRoot = Join-Path $TestDrive 'results'
                $result = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,nodejs-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot

                $result.Status | Should -Be 'Passed'
                Test-Path $result.ManifestPath | Should -BeTrue
                Test-Path $result.SummaryPath | Should -BeTrue
                Test-Path $result.TestResultPath | Should -BeTrue
                Assert-MockCalled Apply-DistroNexusTemplate -Times 0 -ModuleName DistroNexus
            }
        }

        It "Should execute apply and runtime probes in non-dry-run mode" {
            InModuleScope DistroNexus {
                Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                Mock Get-DistroNexusTemplate {
                    return @(
                        [pscustomobject]@{ Id = 'dotnet-dev'; Name = '.NET Development'; ScenarioTags = @('api') }
                    )
                } -ModuleName DistroNexus
                Mock wsl.exe {
                    $global:LASTEXITCODE = 0
                    return 'ok'
                } -ModuleName DistroNexus
                Mock Apply-DistroNexusTemplate { } -ModuleName DistroNexus

                $outputRoot = Join-Path $TestDrive 'results2'
                $result = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev' -Distro 'Ubuntu-22.04' -OutputRoot $outputRoot

                $result.Total | Should -Be 1
                $result.Pass | Should -Be 1
                Assert-MockCalled Apply-DistroNexusTemplate -Times 1 -ModuleName DistroNexus
            }
        }
    }
}
