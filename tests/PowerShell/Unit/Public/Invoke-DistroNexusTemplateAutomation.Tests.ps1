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
                $previousCi = $env:CI
                $env:CI = $null
                try {
                Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                Mock Get-DistroNexusTemplate {
                    return @(
                        [pscustomobject]@{ Id = 'dotnet-dev'; Name = '.NET Development' },
                        [pscustomobject]@{ Id = 'nodejs-dev'; Name = 'Node.js Development' }
                    )
                } -ModuleName DistroNexus

                { Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'unknown-template' -Distro 'Ubuntu-22.04' } | Should -Throw
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }
    }

    Context "Execution and artifacts" {
        It "Should generate manifest, summary and xml in dry-run mode" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
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

                $outputRoot = Join-Path $TestDrive 'results'
                $result = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,nodejs-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot

                $result.Status | Should -Be 'Passed'
                Test-Path $result.ManifestPath | Should -BeTrue
                Test-Path $result.SummaryPath | Should -BeTrue
                Test-Path $result.TestResultPath | Should -BeTrue
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should block capability-gated templates when CpuOnly profile is used" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
                    Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                    Mock Get-DistroNexusTemplate {
                        return @(
                            [pscustomobject]@{ Id = 'ai-ml-gpu-dev'; Name = 'AI/ML GPU Development'; ScenarioTags = @('ai', 'gpu') }
                        )
                    } -ModuleName DistroNexus
                    Mock wsl.exe {
                        $global:LASTEXITCODE = 0
                        return 'ok'
                    } -ModuleName DistroNexus

                    $outputRoot = Join-Path $TestDrive 'results3'
                    $result = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'ai-ml-gpu-dev' -Distro 'Ubuntu-22.04' -DryRun -CapabilityProfile CpuOnly -OutputRoot $outputRoot

                    $result.Blocked | Should -Be 1
                    $result.Results[0].Reason | Should -Match 'Capability profile'
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should invoke diagnostic cmdlet for GpuCapable profile" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
                    Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                    Mock Get-DistroNexusTemplate {
                        return @(
                            [pscustomobject]@{ Id = 'ai-ml-gpu-dev'; Name = 'AI/ML GPU Development'; ScenarioTags = @('ai', 'gpu') }
                        )
                    } -ModuleName DistroNexus
                    Mock wsl.exe {
                        $global:LASTEXITCODE = 0
                        return 'ok'
                    } -ModuleName DistroNexus
                    Mock Test-DistroNexusTemplateEnvironment {
                        return @(
                            [pscustomobject]@{ Capability = 'Gpu'; Status = 'Pass'; Reason = 'ok'; Details = @{} }
                        )
                    } -ModuleName DistroNexus

                    $outputRoot = Join-Path $TestDrive 'results4'
                    $result = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'ai-ml-gpu-dev' -Distro 'Ubuntu-22.04' -DryRun -CapabilityProfile GpuCapable -OutputRoot $outputRoot

                    $result.Pass | Should -Be 1
                    $result.Results[0].CapabilityProfile | Should -Be 'GpuCapable'
                    $result.Results[0].CapabilityDiagnostics.Count | Should -BeGreaterThan 0
                    Assert-MockCalled Test-DistroNexusTemplateEnvironment -Times 1 -ModuleName DistroNexus
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should block non-dry-run execution until the reviewed apply contract is available" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
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

                $outputRoot = Join-Path $TestDrive 'results2'
                $result = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev' -Distro 'Ubuntu-22.04' -OutputRoot $outputRoot

                $result.Total | Should -Be 1
                $result.Blocked | Should -Be 1
                $result.Results[0].Reason | Should -Match 'reviewed preview/execute contract'
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should generate regression diff using latest successful baseline" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
                    Mock Get-Command { return [pscustomobject]@{ Name = 'wsl.exe' } } -ModuleName DistroNexus -ParameterFilter { $Name -eq 'wsl.exe' }
                    Mock Get-DistroNexusTemplate {
                        return @(
                            [pscustomobject]@{ Id = 'dotnet-dev'; Name = '.NET Development'; ScenarioTags = @('api') },
                            [pscustomobject]@{ Id = 'ai-ml-gpu-dev'; Name = 'AI/ML GPU Development'; ScenarioTags = @('ai', 'gpu') }
                        )
                    } -ModuleName DistroNexus
                    Mock wsl.exe {
                        $global:LASTEXITCODE = 0
                        return 'ok'
                    } -ModuleName DistroNexus
                    Mock Test-DistroNexusTemplateEnvironment {
                        return @(
                            [pscustomobject]@{ Capability = 'Gpu'; Status = 'Pass'; Reason = 'ok'; Details = @{} }
                        )
                    } -ModuleName DistroNexus

                    $outputRoot = Join-Path $TestDrive 'results-diff'
                    $baselineRun = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,ai-ml-gpu-dev' -Distro 'Ubuntu-22.04' -DryRun -CapabilityProfile CpuOnly -OutputRoot $outputRoot

                    $currentRun = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,ai-ml-gpu-dev' -Distro 'Ubuntu-22.04' -DryRun -CapabilityProfile GpuCapable -OutputRoot $outputRoot -EnableRegressionDiff

                    Test-Path $currentRun.RegressionDiffPath | Should -BeTrue
                    $diff = Get-Content -Path $currentRun.RegressionDiffPath -Raw | ConvertFrom-Json
                    $diff.HasBaseline | Should -BeTrue
                    $diff.BaselineRunId | Should -Be $baselineRun.RunId
                    $diff.Delta.Pass | Should -Be 1
                    $diff.Delta.Blocked | Should -Be -1
                    @($diff.ChangedItems).Count | Should -BeGreaterThan 0
                    @($diff.ChangedItems | Where-Object { $_.TemplateId -eq 'ai-ml-gpu-dev' }).Count | Should -Be 1
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should resolve explicit baseline run id deterministically" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
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

                    $outputRoot = Join-Path $TestDrive 'results-explicit-baseline'
                    $baselineRun = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,nodejs-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot
                    $currentRun = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,nodejs-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot -EnableRegressionDiff -BaselineRunId $baselineRun.RunId

                    $diff = Get-Content -Path $currentRun.RegressionDiffPath -Raw | ConvertFrom-Json
                    $diff.BaselinePolicy | Should -Be 'ExplicitRunId'
                    $diff.BaselineRunId | Should -Be $baselineRun.RunId
                    $diff.Message | Should -Match 'explicit run ID'
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should produce zero-change regression diff for identical runs" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
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

                    $outputRoot = Join-Path $TestDrive 'results-zero-change'
                    $baselineRun = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot
                    $currentRun = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot -EnableRegressionDiff -BaselineRunId $baselineRun.RunId

                    $diff = Get-Content -Path $currentRun.RegressionDiffPath -Raw | ConvertFrom-Json
                    $summary = Get-Content -Path $currentRun.SummaryPath -Raw

                    $diff.IsZeroChange | Should -BeTrue
                    @($diff.ChangedItems).Count | Should -Be 0
                    $diff.Delta.Pass | Should -Be 0
                    $diff.Delta.Fail | Should -Be 0
                    $diff.Delta.Blocked | Should -Be 0
                    $summary | Should -Match 'No changes detected relative to baseline'
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should represent added and removed templates in regression diff" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
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

                    $outputRoot = Join-Path $TestDrive 'results-added-removed'

                    $baselineAdded = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot
                    $currentAdded = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,nodejs-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot -EnableRegressionDiff -BaselineRunId $baselineAdded.RunId
                    $addedDiff = Get-Content -Path $currentAdded.RegressionDiffPath -Raw | ConvertFrom-Json
                    @($addedDiff.ChangedItems | Where-Object { $_.TemplateId -eq 'nodejs-dev' -and $_.ChangeType -eq 'Added' }).Count | Should -Be 1

                    $baselineRemoved = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev,nodejs-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot
                    $currentRemoved = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot -EnableRegressionDiff -BaselineRunId $baselineRemoved.RunId
                    $removedDiff = Get-Content -Path $currentRemoved.RegressionDiffPath -Raw | ConvertFrom-Json
                    @($removedDiff.ChangedItems | Where-Object { $_.TemplateId -eq 'nodejs-dev' -and $_.ChangeType -eq 'Removed' }).Count | Should -Be 1
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }

        It "Should not fail when explicit baseline run id is missing" {
            InModuleScope DistroNexus {
                $previousCi = $env:CI
                $env:CI = $null
                try {
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

                    $outputRoot = Join-Path $TestDrive 'results-missing-baseline'
                    $run = Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev' -Distro 'Ubuntu-22.04' -DryRun -OutputRoot $outputRoot -EnableRegressionDiff -BaselineRunId 'missing-run-id'

                    $run.Status | Should -Be 'Passed'
                    Test-Path $run.RegressionDiffPath | Should -BeTrue
                    $diff = Get-Content -Path $run.RegressionDiffPath -Raw | ConvertFrom-Json
                    $diff.HasBaseline | Should -BeFalse
                    $diff.BaselinePolicy | Should -Be 'ExplicitRunId'
                    $diff.Message | Should -Match 'not found'
                }
                finally {
                    $env:CI = $previousCi
                }
            }
        }
    }
}
