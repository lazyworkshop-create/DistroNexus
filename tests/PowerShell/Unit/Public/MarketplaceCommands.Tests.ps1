BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
    Import-Module $modulePath -Force
}

Describe 'Marketplace PowerShell commands' {
    It 'exports the source and trust commands' {
        Get-Command Get-DistroNexusTemplateSource -ErrorAction Stop | Should -Not -BeNullOrEmpty
        Get-Command Add-DistroNexusTemplateSource -ErrorAction Stop | Should -Not -BeNullOrEmpty
        Get-Command Set-DistroNexusTemplateSourceEnabled -ErrorAction Stop | Should -Not -BeNullOrEmpty
        Get-Command Remove-DistroNexusTemplateSource -ErrorAction Stop | Should -Not -BeNullOrEmpty
        Get-Command Approve-DistroNexusTemplateMarketplaceCandidate -ErrorAction Stop | Should -Not -BeNullOrEmpty
        Get-Command Get-DistroNexusTemplateMarketplaceArtifactHistory -ErrorAction Stop | Should -Not -BeNullOrEmpty
        Get-Command Get-DistroNexusTemplateMarketplaceScriptDiff -ErrorAction Stop | Should -Not -BeNullOrEmpty
        Get-Command Restore-DistroNexusTemplateMarketplaceArtifact -ErrorAction Stop | Should -Not -BeNullOrEmpty
    }

    It 'returns an explicit WhatIf preview for an insecure source without starting a bridge operation' {
        $preview = Add-DistroNexusTemplateSource -Url 'http://catalog.example.test/templates.json' -WhatIf
        $preview.Operation | Should -Be 'AddTemplateSource'
        $preview.ExplicitConfirmationRequired | Should -BeTrue
    }

    It 'requires an explicit review-shaped WhatIf approval' {
        $preview = Approve-DistroNexusTemplateMarketplaceCandidate -ReviewToken 'core-grant' -WhatIf
        $preview.ExplicitReviewRequired | Should -BeTrue
    }

    It 'returns guarded lifecycle previews without starting the bridge' {
        (Set-DistroNexusTemplateSourceEnabled -SourceId source-1 -Enabled $false -WhatIf).Operation | Should -Be 'SetTemplateSourceEnabled'
        (Remove-DistroNexusTemplateSource -SourceId source-1 -WhatIf).ExplicitConfirmationRequired | Should -BeTrue
    }

    It 'returns a guarded rollback preview without starting the bridge' {
        $preview = Restore-DistroNexusTemplateMarketplaceArtifact -TemplateId demo -Sha256 ('a' * 64) -WhatIf
        $preview.Operation | Should -Be 'RestoreTemplateMarketplaceArtifact'
        $preview.ExplicitConfirmationRequired | Should -BeTrue
    }

    It 'preserves an exact selected catalog identity in the download WhatIf contract' {
        $digest = 'b' * 64
        $preview = Save-DistroNexusTemplateMarketplaceArtifact -SourceId source-2 -TemplateId second-template -ManifestDigest $digest -WhatIf
        $preview.SourceId | Should -Be 'source-2'
        $preview.TemplateId | Should -Be 'second-template'
        $preview.ManifestDigest | Should -Be $digest
    }

    It 'preserves a Core marketplace error code through the bridge and PowerShell adapter' {
        { Get-DistroNexusTemplateMarketplaceScriptDiff -TemplateId ('missing-' + [guid]::NewGuid().ToString('N')) -Sha256 ('a' * 64) -ErrorAction Stop } | Should -Throw '*TemplateNotFound*'
    }
}
