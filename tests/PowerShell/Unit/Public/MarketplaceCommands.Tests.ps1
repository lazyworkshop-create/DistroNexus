BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
    Import-Module $modulePath -Force
}

Describe 'Marketplace PowerShell commands' {
    $readCommands = @(
        'Get-DistroNexusTemplateSource',
        'Get-DistroNexusTemplateMarketplaceEntry',
        'Get-DistroNexusTemplateMarketplaceStatus',
        'Get-DistroNexusTemplateMarketplaceHistory'
    )
    $mutationCommands = @(
        'Add-DistroNexusTemplateSource',
        'Set-DistroNexusTemplateSource',
        'Remove-DistroNexusTemplateSource',
        'Get-DistroNexusTemplateMarketplaceReview',
        'Approve-DistroNexusTemplateMarketplaceCandidate',
        'Save-DistroNexusTemplateMarketplaceArtifact',
        'Restore-DistroNexusTemplateMarketplaceArtifact'
    )

    It 'exports exactly the fixed v1 marketplace command family' {
        foreach ($commandName in $readCommands + $mutationCommands) {
            Get-Command $commandName -ErrorAction Stop | Should -Not -BeNullOrEmpty
        }
        foreach ($retired in 'Set-DistroNexusTemplateSourceEnabled', 'Get-DistroNexusTemplateMarketplaceReviewGrant', 'Get-DistroNexusTemplateMarketplaceArtifactHistory', 'Get-DistroNexusTemplateMarketplaceScriptDiff') {
            Get-Command $retired -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        }
    }

    It 'marks every marketplace mutation as ShouldProcess guarded' {
        foreach ($commandName in $mutationCommands) {
            (Get-Command $commandName -ErrorAction Stop).SupportsShouldProcess | Should -BeTrue
        }
    }

    It 'keeps the public adapter on fixed versioned Bridge operations only' {
        $path = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\MarketplaceCommands.ps1'
        $content = Get-Content $path -Raw
        $content | Should -Match "template\.marketplace\.sources\.v1"
        $content | Should -Match "template\.marketplace\.review\.v1"
        $content | Should -Not -Match "marketplaceScriptDiff|marketplaceCreateReviewGrant|marketplaceDownloadArtifact"
    }

    It 'validates exact marketplace identity before invoking the bridge' {
        { Get-DistroNexusTemplateMarketplaceStatus -SourceId source -TemplateId template -ManifestDigest short -ErrorAction Stop } | Should -Throw
        { Approve-DistroNexusTemplateMarketplaceCandidate -ReviewToken short -ErrorAction Stop } | Should -Throw
    }
}
