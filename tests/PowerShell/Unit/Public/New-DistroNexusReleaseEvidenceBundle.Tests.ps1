# New-DistroNexusReleaseEvidenceBundle.Tests.ps1

Describe "New-DistroNexusReleaseEvidenceBundle" -Tag 'Unit', 'Public' {

    BeforeAll {
        $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
        $modulePath = Join-Path $rootPath "src\PowerShell"
        Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    }

    It "Should build deterministic checklist mapping with unresolved items" {
        InModuleScope DistroNexus {
            $outputPath = Join-Path $TestDrive 'evidence\v2.1.1-evidence.json'

            $params = @{
                ReleaseVersion = 'v2.1.1'
                WorkflowRuns = @('https://github.com/org/repo/actions/runs/100?token=secret')
                TestArtifacts = @('invalid-link')
                ReleaseLinks = @('https://github.com/org/repo/releases/tag/v2.1.1')
                OutputPath = $outputPath
            }

            $result = New-DistroNexusReleaseEvidenceBundle @params

            $result.Status | Should -Be 'CompletedWithUnresolved'
            Test-Path $result.OutputPath | Should -BeTrue

            $bundle = Get-Content -Path $result.OutputPath -Raw | ConvertFrom-Json
            $bundle.ReleaseVersion | Should -Be 'v2.1.1'
            $bundle.Summary.TotalItems | Should -Be 3
            $bundle.Summary.Unresolved | Should -Be 1
            @($bundle.ChecklistMapping | Where-Object { $_.Section -eq 'BuildAndPackaging' }).Count | Should -Be 1
            @($bundle.UnresolvedItems).Count | Should -Be 1

            $workflowItem = @($bundle.Items | Where-Object { $_.SourceType -eq 'WorkflowRun' })[0]
            $workflowItem.Link | Should -Be 'https://github.com/org/repo/actions/runs/100'
        }
    }

    It "Should preserve manual overrides and classify pending entries" {
        InModuleScope DistroNexus {
            $outputPath = Join-Path $TestDrive 'evidence\manual-evidence.json'
            $manualOverrides = @(
                [pscustomobject]@{ Section = 'StoreSubmission'; Title = 'Store package review'; Link = ''; PendingReason = 'Waiting partner center URL' },
                [pscustomobject]@{ Section = 'ReleaseNotesAndDistribution'; Title = 'Website blog post'; Link = 'https://example.com/blog/release-v2-1-1' }
            )

            $result = New-DistroNexusReleaseEvidenceBundle -ReleaseVersion 'v2.1.1' -ManualOverrides $manualOverrides -OutputPath $outputPath

            $result.Summary.TotalItems | Should -Be 2
            $result.Summary.Unresolved | Should -Be 1

            $bundle = Get-Content -Path $outputPath -Raw | ConvertFrom-Json
            @($bundle.Items | Where-Object { $_.IsManualOverride -eq $true }).Count | Should -Be 2
            @($bundle.UnresolvedItems | Where-Object { $_.Section -eq 'StoreSubmission' }).Count | Should -Be 1
        }
    }

    It "Should produce stable item IDs for same input across reruns" {
        InModuleScope DistroNexus {
            $outputA = Join-Path $TestDrive 'evidence\run-a.json'
            $outputB = Join-Path $TestDrive 'evidence\run-b.json'

            $params = @{
                ReleaseVersion = 'v2.1.1'
                WorkflowRuns = @('https://example.com/workflow/1')
                TestArtifacts = @('https://example.com/tests/1')
                ReleaseLinks = @('https://example.com/release/1')
            }

            $runA = New-DistroNexusReleaseEvidenceBundle @params -OutputPath $outputA
            $runB = New-DistroNexusReleaseEvidenceBundle @params -OutputPath $outputB

            $bundleA = Get-Content -Path $runA.OutputPath -Raw | ConvertFrom-Json
            $bundleB = Get-Content -Path $runB.OutputPath -Raw | ConvertFrom-Json

            $idsA = @($bundleA.Items | ForEach-Object { $_.Id } | Sort-Object)
            $idsB = @($bundleB.Items | ForEach-Object { $_.Id } | Sort-Object)
            ($idsA -join ',') | Should -Be ($idsB -join ',')
        }
    }
}
